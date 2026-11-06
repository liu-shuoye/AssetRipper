#pragma once

#include <stdint.h>

// acl-native：ACL v2.0.0 compressed_tracks 的最小 C ABI。
// 调用方负责在 aclv_open 到 aclv_close 期间保持 data 指针有效（DLL 不复制 blob）。
// 输出数组均按 track 内 sample 连续布局：
//   rotation:    sample * 4 + component (x,y,z,w)
//   translation: sample * 3 + component
//   scale:       sample * 3 + component

#if defined(_WIN32)
#if defined(ACLV_BUILD_DLL)
#define ACLV_API extern "C" __declspec(dllexport)
#else
#define ACLV_API extern "C" __declspec(dllimport)
#endif
#else
#define ACLV_API extern "C"
#endif

typedef struct AclvHeaderInfo
{
	uint32_t size;
	uint32_t hash;
	uint16_t version;
	uint8_t algorithm_type;
	uint8_t track_type;
	uint32_t num_tracks;
	uint32_t num_samples;
	float sample_rate;
	float duration;
	uint32_t num_segments;
	uint32_t animated_rotation_sub_tracks;
	uint32_t animated_translation_sub_tracks;
	uint32_t animated_scale_sub_tracks;
	uint32_t constant_rotation_samples;
	uint32_t constant_translation_samples;
	uint32_t constant_scale_samples;
	uint8_t has_scale;
	uint8_t default_scale;
	int32_t error_code;
	char error_message[256];
} AclvHeaderInfo;

// 无状态校验：只检查头部/hash，不建立解压上下文。
ACLV_API int aclv_probe(const void* data, uint32_t size, AclvHeaderInfo* out_info);

// 建立解压上下文。成功后 handle 必须由 aclv_close 释放。
ACLV_API int aclv_open(const void* data, uint32_t size, void** out_handle, AclvHeaderInfo* out_info);

ACLV_API void aclv_close(void* handle);

// 0=default，1=constant，2=animated。
ACLV_API int aclv_get_sub_track_types(void* handle, uint8_t* rotation_types, uint8_t* translation_types, uint8_t* scale_types, uint32_t track_capacity);

// 只解一条 track，避免一次性大拷贝；数组容量需 >= num_samples。
ACLV_API int aclv_decompress_track(void* handle, uint32_t track_index, float* rotations, float* translations, float* scales, uint32_t sample_capacity);
