#include "acl_native.h"

#include "acl/core/compressed_tracks.h"
#include "acl/core/floating_point_exceptions.h"
#include "acl/decompression/decompress.h"

#include <algorithm>
#include <cstddef>
#include <cstdio>
#include <cstring>
#include <new>

namespace
{
	void set_error(AclvHeaderInfo* out_info, int32_t code, const char* message)
	{
		if (out_info == nullptr)
		{
			return;
		}
		out_info->error_code = code;
		std::snprintf(out_info->error_message, sizeof(out_info->error_message), "%s", message == nullptr ? "" : message);
	}

	void clear_info(AclvHeaderInfo* out_info)
	{
		std::memset(out_info, 0, sizeof(AclvHeaderInfo));
	}

	void fill_info(const acl::compressed_tracks& tracks, AclvHeaderInfo* out_info)
	{
		const acl::acl_impl::tracks_header& header = acl::acl_impl::get_tracks_header(tracks);
		const acl::acl_impl::transform_tracks_header& transform = acl::acl_impl::get_transform_tracks_header(tracks);

		out_info->size = tracks.get_size();
		out_info->hash = tracks.get_hash();
		out_info->version = static_cast<uint16_t>(tracks.get_version());
		out_info->algorithm_type = static_cast<uint8_t>(tracks.get_algorithm_type());
		out_info->track_type = static_cast<uint8_t>(tracks.get_track_type());
		out_info->num_tracks = tracks.get_num_tracks();
		out_info->num_samples = tracks.get_num_samples_per_track();
		out_info->sample_rate = tracks.get_sample_rate();
		out_info->duration = tracks.get_duration();
		out_info->num_segments = transform.num_segments;
		out_info->animated_rotation_sub_tracks = transform.num_animated_rotation_sub_tracks;
		out_info->animated_translation_sub_tracks = transform.num_animated_translation_sub_tracks;
		out_info->animated_scale_sub_tracks = transform.num_animated_scale_sub_tracks;
		out_info->constant_rotation_samples = transform.num_constant_rotation_samples;
		out_info->constant_translation_samples = transform.num_constant_translation_samples;
		out_info->constant_scale_samples = transform.num_constant_scale_samples;
		out_info->has_scale = header.get_has_scale() ? 1 : 0;
		out_info->default_scale = static_cast<uint8_t>(header.get_default_scale());
	}

	struct AclvHandle
	{
		const acl::compressed_tracks* tracks = nullptr;
		acl::decompression_context<acl::default_transform_decompression_settings>* context = nullptr;
		AclvHeaderInfo info = {};
	};

	// 逐 track 解压时只写当前 track 的当前 sample。
	struct SingleTrackWriter final : public acl::track_writer
	{
		float* rotations;
		float* translations;
		float* scales;
		uint32_t sample_index;

		void RTM_SIMD_CALL write_rotation(uint32_t /*track_index*/, rtm::quatf_arg0 rotation)
		{
			rtm::quat_store(rotation, rotations + static_cast<size_t>(sample_index) * 4);
		}

		void RTM_SIMD_CALL write_translation(uint32_t /*track_index*/, rtm::vector4f_arg0 translation)
		{
			rtm::vector_store3(translation, translations + static_cast<size_t>(sample_index) * 3);
		}

		void RTM_SIMD_CALL write_scale(uint32_t /*track_index*/, rtm::vector4f_arg0 scale)
		{
			rtm::vector_store3(scale, scales + static_cast<size_t>(sample_index) * 3);
		}
	};
}

ACLV_API int aclv_probe(const void* data, uint32_t size, AclvHeaderInfo* out_info)
{
	if (out_info == nullptr)
	{
		return -1;
	}
	clear_info(out_info);

	if (data == nullptr)
	{
		set_error(out_info, -2, "data is null");
		return -2;
	}
	if (size < 16)
	{
		set_error(out_info, -3, "buffer too small for ACL header");
		return -3;
	}

	acl::error_result result;
	const acl::compressed_tracks* tracks = acl::make_compressed_tracks(data, &result);
	if (tracks == nullptr)
	{
		set_error(out_info, -4, result.c_str());
		return -4;
	}

	result = tracks->is_valid(true);
	if (result.any())
	{
		set_error(out_info, -5, result.c_str());
		return -5;
	}

	fill_info(*tracks, out_info);
	return 0;
}

ACLV_API int aclv_open(const void* data, uint32_t size, void** out_handle, AclvHeaderInfo* out_info)
{
	if (out_handle == nullptr)
	{
		return -1;
	}
	*out_handle = nullptr;

	const int probe_result = aclv_probe(data, size, out_info);
	if (probe_result != 0)
	{
		return probe_result;
	}

	const acl::compressed_tracks* tracks = acl::make_compressed_tracks(data, nullptr);
	if (tracks == nullptr)
	{
		set_error(out_info, -9, "unexpected null compressed_tracks");
		return -9;
	}

	AclvHandle* handle = new (std::nothrow) AclvHandle();
	if (handle == nullptr)
	{
		set_error(out_info, -10, "out of memory for handle");
		return -10;
	}
	handle->tracks = tracks;
	handle->info = *out_info;

	handle->context = new (std::nothrow) acl::decompression_context<acl::default_transform_decompression_settings>();
	if (handle->context == nullptr)
	{
		delete handle;
		set_error(out_info, -11, "out of memory for context");
		return -11;
	}

	if (!handle->context->initialize(*tracks))
	{
		delete handle->context;
		delete handle;
		set_error(out_info, -12, "decompression_context initialize failed");
		return -12;
	}

	*out_handle = handle;
	return 0;
}

ACLV_API void aclv_close(void* handle)
{
	if (handle == nullptr)
	{
		return;
	}
	AclvHandle* typed = static_cast<AclvHandle*>(handle);
	delete typed->context;
	delete typed;
}

ACLV_API int aclv_get_sub_track_types(void* handle, uint8_t* rotation_types, uint8_t* translation_types, uint8_t* scale_types, uint32_t track_capacity)
{
	if (handle == nullptr || rotation_types == nullptr || translation_types == nullptr || scale_types == nullptr)
	{
		return -13;
	}
	AclvHandle* typed = static_cast<AclvHandle*>(handle);
	const uint32_t num_tracks = typed->info.num_tracks;
	if (track_capacity < num_tracks)
	{
		return -14;
	}

	const acl::acl_impl::transform_tracks_header& transform = acl::acl_impl::get_transform_tracks_header(*typed->tracks);
	const uint32_t num_entries = (num_tracks + 15) / 16;
	const acl::acl_impl::packed_sub_track_types* rotation_entries = transform.get_sub_track_types();
	const acl::acl_impl::packed_sub_track_types* translation_entries = rotation_entries + num_entries;
	const acl::acl_impl::packed_sub_track_types* scale_entries = translation_entries + num_entries;

	for (uint32_t track_index = 0; track_index < num_tracks; ++track_index)
	{
		const uint32_t entry_index = track_index / 16;
		const uint32_t shift = 30 - 2 * (track_index % 16);
		rotation_types[track_index] = static_cast<uint8_t>((rotation_entries[entry_index].types >> shift) & 3);
		translation_types[track_index] = static_cast<uint8_t>((translation_entries[entry_index].types >> shift) & 3);
		scale_types[track_index] = static_cast<uint8_t>((scale_entries[entry_index].types >> shift) & 3);
	}

	return 0;
}

ACLV_API int aclv_decompress_track(void* handle, uint32_t track_index, float* rotations, float* translations, float* scales, uint32_t sample_capacity)
{
	if (handle == nullptr || rotations == nullptr || translations == nullptr || scales == nullptr)
	{
		return -15;
	}
	AclvHandle* typed = static_cast<AclvHandle*>(handle);
	if (track_index >= typed->info.num_tracks)
	{
		return -16;
	}
	if (sample_capacity < typed->info.num_samples)
	{
		return -17;
	}

	acl::scope_disable_fp_exceptions fp_off;

	SingleTrackWriter writer;
	writer.rotations = rotations;
	writer.translations = translations;
	writer.scales = scales;

	const float sample_rate = typed->info.sample_rate;
	const float duration = typed->info.duration;
	for (uint32_t sample_index = 0; sample_index < typed->info.num_samples; ++sample_index)
	{
		float time = static_cast<float>(sample_index) / sample_rate;
		time = std::min(time, duration);
		typed->context->seek(time, acl::sample_rounding_policy::nearest);
		writer.sample_index = sample_index;
		typed->context->decompress_track(track_index, writer);
	}

	return 0;
}
