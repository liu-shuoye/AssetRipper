using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Generics;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_1001480554;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_18;
using AssetRipper.SourceGenerated.Classes.ClassID_74;
using AssetRipper.SourceGenerated.Classes.ClassID_91;
using AssetRipper.SourceGenerated.Classes.ClassID_93;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.MarkerInterfaces;
using AssetRipper.SourceGenerated.Subclasses.AnimatorControllerLayer;
using AssetRipper.SourceGenerated.Subclasses.AnimatorControllerParameter;
using AssetRipper.SourceGenerated.Subclasses.ControllerConstant;
using AssetRipper.SourceGenerated.Subclasses.OffsetPtr_LayerConstant;
using AssetRipper.SourceGenerated.Subclasses.OffsetPtr_StateMachineConstant;
using AssetRipper.SourceGenerated.Subclasses.PPtr_AnimationClip;
using AssetRipper.SourceGenerated.Subclasses.PPtr_EditorExtension;
using AssetRipper.SourceGenerated.Subclasses.PPtr_MonoBehaviour;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Prefab;
using AssetRipper.SourceGenerated.Subclasses.PPtr_PrefabInstance;
using AssetRipper.SourceGenerated.Subclasses.StateMachineBehaviourVectorDescription;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class AnimatorController_Nikki4 : RuntimeAnimatorController_2018_3,
	IAnimatorController
{
	private AnimatorController_2018_3 m_animatorController;


	public AnimatorController_Nikki4(AssetInfo info) : base(info)
	{
		m_animatorController = new AnimatorController_2018_3(info);
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		m_animatorController.Name = reader.ReadRelease_Utf8StringAlign();
		m_animatorController.ControllerSize = reader.ReadUInt32();
		// m_animatorController.Controller.ReadRelease(ref reader);
		var controller = (ControllerConstant_2018_2)m_animatorController.Controller;
		controller.LayerArray.ReadRelease_Array_Asset<OffsetPtr_LayerConstant_2018_2>(ref reader);
		
		controller.StateMachineArray.ReadRelease_Array_Asset<OffsetPtr_StateMachineConstant_2018_2>(ref reader);
		controller.Values.ReadRelease(ref reader);
		controller.DefaultValues.ReadRelease(ref reader);
		m_animatorController.TOS.ReadRelease_Map_UInt32_Utf8StringAlign(ref reader);
		m_animatorController.AnimationClips.ReadRelease_ArrayAlign_Asset<PPtr_AnimationClip_5>(ref reader);
		m_animatorController.StateMachineBehaviourVectorDescription.ReadRelease(ref reader);
		m_animatorController.StateMachineBehaviours.ReadRelease_ArrayAlign_Asset<PPtr_MonoBehaviour_5>(ref reader);
		m_animatorController.MultiThreadedStateMachine = reader.ReadRelease_BooleanAlign();
	}

	public bool Has_EvaluateTransitionsOnStart() => m_animatorController.Has_EvaluateTransitionsOnStart();

	public bool Has_MultiThreadedStateMachine() => m_animatorController.Has_MultiThreadedStateMachine();

	public bool Has_PrefabAsset() => m_animatorController.Has_PrefabAsset();

	public bool Has_PrefabInstance() => m_animatorController.Has_PrefabInstance();

	public bool Has_PrefabInternal() => m_animatorController.Has_PrefabInternal();

	public bool Has_StateMachineBehaviours() => m_animatorController.Has_StateMachineBehaviours();

	public bool Has_StateMachineBehaviourVectorDescription() => m_animatorController.Has_StateMachineBehaviourVectorDescription();

	public bool IsReleaseOnly_MultiThreadedStateMachine() => m_animatorController.IsReleaseOnly_MultiThreadedStateMachine();

	public void CopyValues(IAnimatorController source, PPtrConverter converter) => m_animatorController.CopyValues(source, converter);

	public void CopyValues(IAnimatorController source) => m_animatorController.CopyValues(source);

	public AccessListBase<IPPtr_AnimationClip> AnimationClips => m_animatorController.AnimationClips;
	public AccessListBase<IAnimatorControllerLayer> AnimatorLayers => m_animatorController.AnimatorLayers;
	public AccessListBase<IAnimatorControllerParameter> AnimatorParameters => m_animatorController.AnimatorParameters;
	public IControllerConstant Controller => m_animatorController.Controller;

	public uint ControllerSize
	{
		get => m_animatorController.ControllerSize;
		set => m_animatorController.ControllerSize = value;
	}

	public IPPtr_EditorExtension CorrespondingSourceObject => m_animatorController.CorrespondingSourceObject;

	public bool EvaluateTransitionsOnStart
	{
		get => m_animatorController.EvaluateTransitionsOnStart;
		set => m_animatorController.EvaluateTransitionsOnStart = value;
	}

	public uint HideFlags
	{
		get => m_animatorController.HideFlags;
		set => m_animatorController.HideFlags = value;
	}

	public bool MultiThreadedStateMachine
	{
		get => m_animatorController.MultiThreadedStateMachine;
		set => m_animatorController.MultiThreadedStateMachine = value;
	}

	public Utf8String Name_R
	{
		get => m_animatorController.Name_R;
		set => m_animatorController.Name_R = value;
	}

	public PPtr_Prefab_2018_3? PrefabAsset => m_animatorController.PrefabAsset;
	public PPtr_PrefabInstance? PrefabInstance => m_animatorController.PrefabInstance;
	public IPPtr_Prefab? PrefabInternal => m_animatorController.PrefabInternal;
	public AssetList<PPtr_MonoBehaviour_5>? StateMachineBehaviours => m_animatorController.StateMachineBehaviours;
	public IStateMachineBehaviourVectorDescription? StateMachineBehaviourVectorDescription => m_animatorController.StateMachineBehaviourVectorDescription;
	public AssetDictionary<uint, Utf8String> TOS => m_animatorController.TOS;

	public HideFlags HideFlagsE
	{
		get => m_animatorController.HideFlagsE;
		set => m_animatorController.HideFlagsE = value;
	}

	public PPtrAccessList<IPPtr_AnimationClip, IAnimationClip> AnimationClipsP => m_animatorController.AnimationClipsP;

	public IEditorExtension? CorrespondingSourceObjectP
	{
		get => m_animatorController.CorrespondingSourceObjectP;
		set => m_animatorController.CorrespondingSourceObjectP = value;
	}

	public IPrefab? PrefabAssetP
	{
		get => m_animatorController.PrefabAssetP;
		set => m_animatorController.PrefabAssetP = value;
	}

	public IPrefabInstance? PrefabInstanceP
	{
		get => m_animatorController.PrefabInstanceP;
		set => m_animatorController.PrefabInstanceP = value;
	}

	public IPrefabMarker? PrefabInternalP
	{
		get => m_animatorController.PrefabInternalP;
		set => m_animatorController.PrefabInternalP = value;
	}

	public PPtrAccessList<PPtr_MonoBehaviour_5, IMonoBehaviour> StateMachineBehavioursP => m_animatorController.StateMachineBehavioursP;
}
