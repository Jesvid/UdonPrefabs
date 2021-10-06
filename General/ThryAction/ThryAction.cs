﻿
using UdonSharp;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
#endif
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using System;
using System.Linq;

namespace Thry.General
{
    public class ThryAction : UdonSharpBehaviour
    {
        const int ACTION_TYPE_EVENT = 0;
        const int ACTION_TYPE_BOOL = 1;
        const int ACTION_TYPE_FLOAT = 2;

        public int actionType;

        //Normal or Mirror Manager
        const int SPECIAL_ACTION_TYPE_NORMAL = 0;
        const int SPECIAL_ACTION_TYPE_MIRROR_MANAGER = 1;

        public int specialActionType;

        //Special Sync type
        const int HIVE_NONE = 0;
        const int HIVE_MASTER = 1;
        const int HIVE_REMOTE = 2;

        public int hiveType;
        public ThryAction _master;
        public ThryAction[] _remotes;

        //Syncing
        public bool is_synced;

        [UdonSynced]
        public bool synced_bool;

        [UdonSynced]
        public float synced_float;

        public bool prev_local_bool;
        public bool local_bool;
        public float local_float;


        //Clapper
        public bool isClapperAction;
        public int requiredClaps;
        public string desktopKey;

        //Action Togggles
        public GameObject[] toggleObjects;
        public GameObject[] toggleObjectsInverted;
        public VRC_Pickup[] togglePickups;
        public Collider[] toggleColliders;
        //Action Teleport
        public Transform teleportTarget;
        //Action Script Calls
        public GameObject[] udonBehaviours;
        public string[] udonEventNames;
        public string[] udonValueNames;

        //Udonbehevaiour sorting
        GameObject[] udon_bool_Behvaiours;
        string[] udon_bool_Names;
        GameObject[] udon_float_Behvaiours;
        string[] udon_float_Names;
        GameObject[] udon_int_Behvaiours;
        string[] udon_int_Names;
        GameObject[] udon_event_Behvaiours;
        string[] udon_event_Names;

        //Action Animator driver
        public Animator[] animators;
        public int[] animatorParameterTypes;
        public string[] animatorParameterNames;

        //Requirement Colliders
        public Collider[] hasToBeInsideAnyCollider;
        //Requirement Master
        public bool hasToBeMaster;
        //Requirement Instance Owner
        public bool hasToBeInstanceOwner;
        //Requirement Object Owner
        public bool hasToBeOwner;
        public GameObject hasToBeOwnerGameobject;
        //Requirment PlayerList
        public string[] autherizedPlayerDisplayNames;
        private bool isAutherizedPlayer;

        //Mirror Manager
        public Transform[] mirrors;
        public float maximumOpenDistance = 5;

        //Adapter
        UdonBehaviour _adapter;
        bool hasAdapter;

        private Transform selectedMirror;

        bool hasStartNotRun = true;
        bool doBlockOnInteract = false;

        private void Init()
        {
            //==>Actions
            //Sort udon behaviours
            int floatB = 0; int intB = 0; int boolB = 0; int eventB = 0;
            for (int i = 0; i < udonBehaviours.Length; i++)
            {
                if (Utilities.IsValid(udonBehaviours[i]))
                {
                    UdonBehaviour u = (UdonBehaviour)udonBehaviours[i].GetComponent(typeof(UdonBehaviour));
                    if (Utilities.IsValid(u))
                    {
                        if (i < udonEventNames.Length && udonEventNames[i]!=null && udonEventNames[i].Length > 0) eventB++;
                        if (i < udonValueNames.Length && udonValueNames[i]!=null && udonValueNames[i].Length > 0)
                        {
                            if (u.GetProgramVariableType(udonValueNames[i]) == typeof(float)) floatB++;
                            if (u.GetProgramVariableType(udonValueNames[i]) == typeof(int)) intB++;
                            if (u.GetProgramVariableType(udonValueNames[i]) == typeof(bool)) boolB++;
                        }
                    }
                }
            }
            udon_bool_Behvaiours = new GameObject[boolB]; udon_bool_Names = new string[boolB];
            udon_float_Behvaiours = new GameObject[floatB]; udon_float_Names = new string[floatB];
            udon_int_Behvaiours = new GameObject[intB]; udon_int_Names = new string[intB];
            udon_event_Behvaiours = new GameObject[eventB]; udon_event_Names = new string[eventB];
            floatB = intB = boolB = eventB = 0;
            for (int i = 0; i < udonBehaviours.Length; i++)
            {
                if (Utilities.IsValid(udonBehaviours[i]))
                {
                    UdonBehaviour u = (UdonBehaviour)udonBehaviours[i].GetComponent(typeof(UdonBehaviour));
                    if (Utilities.IsValid(u))
                    {
                        if (i < udonEventNames.Length && udonEventNames[i]!=null && udonEventNames[i].Length > 0)
                        {
                            udon_event_Behvaiours[eventB] = udonBehaviours[i];
                            udon_event_Names[eventB++] = udonEventNames[i];
                        }
                        if (i < udonValueNames.Length && udonValueNames[i] != null && udonValueNames[i].Length > 0)
                        {
                            if (u.GetProgramVariableType(udonValueNames[i]) == typeof(float))
                            {
                                udon_float_Behvaiours[floatB] = udonBehaviours[i];
                                udon_float_Names[floatB++] = udonValueNames[i];
                            }
                            if (u.GetProgramVariableType(udonValueNames[i]) == typeof(int))
                            {
                                udon_int_Behvaiours[intB] = udonBehaviours[i];
                                udon_int_Names[intB++] = udonValueNames[i];
                            }
                            if (u.GetProgramVariableType(udonValueNames[i]) == typeof(bool))
                            {
                                udon_bool_Behvaiours[eventB] = udonBehaviours[i];
                                udon_bool_Names[eventB++] = udonValueNames[i];
                            }
                        }
                    }
                }
            }

            //==>Requirements
            //Check Autherized name list
            string localName = Networking.LocalPlayer.displayName;
            foreach (string n in autherizedPlayerDisplayNames) if (localName == n) isAutherizedPlayer = true;
            if (autherizedPlayerDisplayNames.Length == 0) isAutherizedPlayer = true;

            //==>Others
            foreach (Component u in gameObject.GetComponents(typeof(UdonBehaviour)))
            {
                if (u != this && true.Equals(((UdonBehaviour)u).GetProgramVariable("_isThryAdapter")))
                {
                    _adapter = (UdonBehaviour)u;
                    hasAdapter = true;
                }
            }

            //Update values
            _UpdateFromAdapter();

            //Register remote client
            if (hiveType == HIVE_REMOTE)
            {
                _master.RegisterRemote(this);
                _master.PublicInit();//make sure master is initalized before getting it's data
                SyncValuesFromMaster();
            }

            Serialize();

            //Update all reference values
            _UpdateValues();

            hasStartNotRun = false;
        }

        public void PublicInit()
        {
            if (hasStartNotRun) Init();
        }

        private void Start()
        {
            if (hasStartNotRun) Init();
        }

        private void OnEnable()
        {
            if (hasStartNotRun) return;
            //Animators forget their params after they been disabled. This restores them after reenabling.
            UpdateBoolAnimators();
            UpdateFloatAnimators();
        }

        public void RegisterRemote(ThryAction r)
        {
            if (_remotes == null)
            {
                _remotes = new ThryAction[1];
            }
            else
            {
                ThryAction[] old = _remotes;
                _remotes = new ThryAction[old.Length + 1];
                Array.Copy(old, _remotes, old.Length);
            }
            _remotes[_remotes.Length - 1] = r;
        }

        public void _UpdateToAdapter()
        {
            if (!hasAdapter) return;
            doBlockOnInteract = true; //Block interaction to prevent the ui triggering another OnInteraction event
            if(actionType == ACTION_TYPE_BOOL)
            {
                _adapter.SetProgramVariable("local_bool", local_bool);
                _adapter.SendCustomEvent("SetLocalBool");
            }else if(actionType == ACTION_TYPE_FLOAT)
            {
                _adapter.SetProgramVariable("local_float", local_float);
                _adapter.SendCustomEvent("SetLocalFloat");
            }
            doBlockOnInteract = false;
        }

        public bool _UpdateFromAdapter()
        {
            Debug.Log("Adapter: " + hasAdapter);
            Debug.Log("Type: " + actionType);
            Debug.Log("Local: " + local_bool);
            Debug.Log("Sync: " + synced_bool);
            if (!hasAdapter) return false;
            if (actionType == ACTION_TYPE_BOOL)
            {
                bool prev = local_bool;
                _adapter.SendCustomEvent("GetLocalBool");
                local_bool = (bool)_adapter.GetProgramVariable("local_bool");
                return prev != local_bool;
            }
            else if (actionType == ACTION_TYPE_FLOAT)
            {
                float prev = local_float;
                _adapter.SendCustomEvent("GetLocalFloat");
                local_float = (float)_adapter.GetProgramVariable("local_float");
                return prev != local_float;
            }
            return false;
        }

        //========Exposed Call==========

        public override void Interact()
        {
            OnInteraction();
        }

        public void OnInteraction()
        {
            Debug.Log("Block:" + doBlockOnInteract);
            if (doBlockOnInteract) return;
            if (specialActionType == 0)
            {
                Debug.Log("Req: " + IsNormalRequirementMet());
                if (IsNormalRequirementMet()) UpdateAndExecuteOnInteraction();
                else _UpdateToAdapter();
            }
            if (specialActionType == 1 && IsMirrorRequirementMet()) _ExecuteMirror();
        }

        //=========Mirror manager=========

        private bool IsMirrorRequirementMet()
        {
            float closestTransform = float.MaxValue;
            Transform selected = null;
            VRCPlayerApi.TrackingData trackingData = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

            Vector3 lookDirection = (trackingData.rotation * Vector3.forward).normalized;
            Ray lookRay = new Ray(trackingData.position, lookDirection);
            foreach (Transform t in mirrors)
            {
                Plane mirrorPlane = new Plane(t.rotation * Vector3.back, t.position);
                float intersectionDistance;
                if (mirrorPlane.Raycast(lookRay, out intersectionDistance) == false)
                    continue;

                Vector3 intersection = trackingData.position + lookDirection * intersectionDistance;

                Vector3 toIntersectionVector = intersection - t.position;
                toIntersectionVector = Quaternion.Inverse(t.rotation) * toIntersectionVector;

                if (Mathf.Abs(toIntersectionVector.x) > t.lossyScale.x / 2 || Mathf.Abs(toIntersectionVector.y) > t.lossyScale.y / 2)
                    continue;

                if (intersectionDistance < closestTransform)
                {
                    closestTransform = intersectionDistance;
                    selected = t;
                }
            }

            if (selected != null && closestTransform < maximumOpenDistance)
            {
                //Open new mirror
                //Turn old mirror off
                if (selectedMirror != selected)
                {
                    if (selectedMirror != null) selectedMirror.gameObject.SetActive(false);
                    selectedMirror = selected;
                }
                return true;
            }
            return selectedMirror != null && selectedMirror.gameObject.activeSelf;
        }

        private void _ExecuteMirror()
        {
            selectedMirror.gameObject.SetActive(!selectedMirror.gameObject.activeSelf);
        }

        //=======Requirements========

        private bool IsNormalRequirementMet()
        {
            return IsInsideColliderMet() && isAutherizedPlayer
                && (!hasToBeMaster || Networking.IsMaster) //Master requirement 
                && (!hasToBeInstanceOwner || Networking.IsInstanceOwner) //IsInstanceOwner requirement 
                && (!hasToBeOwner || Networking.IsOwner(hasToBeOwnerGameobject)); //Owner requirement
        }

        private bool IsInsideColliderMet()
        {
            if (hasToBeInsideAnyCollider.Length == 0) return true;
            Vector3 position = Networking.LocalPlayer.GetPosition();
            foreach (Collider c in hasToBeInsideAnyCollider)
            {
                if (c.ClosestPoint(position) == position)
                {
                    return true;
                }
            }
            return false;
        }

        //========Actions==========

        public void Serialize()
        {
            if (is_synced)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                synced_bool = local_bool;
                synced_float = local_float;
                RequestSerialization();
            }
        }

        public void SetBool(bool b)
        {
            if (hasStartNotRun) Init(); // In case it is called in start before this start has been called
            prev_local_bool = local_bool;
            local_bool = b;
            _UpdateToAdapter();
            UpdateAndExecuteOnInteraction_Execute();
        }

        public void SetFloat(float f)
        {
            if (hasStartNotRun) Init(); // In case it is called in start before this start has been called
            prev_local_bool = local_bool;
            local_float = f;
            _UpdateToAdapter();
            UpdateAndExecuteOnInteraction_Execute();
        }

        //Syncing
        public override void OnDeserialization()
        {
            UpdateAndExecuteOnNetworkChange();
        }

        private void UpdateAndExecuteOnNetworkChange()
        {
            if (!is_synced) return;
            prev_local_bool = local_bool;
            if (actionType == ACTION_TYPE_FLOAT)
            {
                if (local_float == synced_float) return;
            }
            else if (actionType == ACTION_TYPE_BOOL)
            {
                if (local_bool == synced_bool) return;
            }
            doBlockOnInteract = true;
            local_float = synced_float;
            local_bool = synced_bool;
            _UpdateToAdapter();
            _UpdateValues();
            ExecuteEvents();
            SyncRemotesIfHiveAndMaster();
            doBlockOnInteract = false;
        }

        private void UpdateAndExecuteOnInteraction()
        {
            prev_local_bool = local_bool;
            if (!UpdateAndExecuteOnInteraction_Update()) return;
            UpdateAndExecuteOnInteraction_Execute();
        }

        private bool UpdateAndExecuteOnInteraction_Update()
        {
            //Update the values from adapter if avaialbe
            if (hasAdapter) return _UpdateFromAdapter();
            local_bool = !local_bool;
            return true;
        }

        private void UpdateAndExecuteOnInteraction_Execute()
        {
            Debug.Log("Execute");
            //Let Master handle it if remote
            if (hiveType == HIVE_REMOTE)
            {
                _master.local_bool = local_bool;
                _master.local_float = local_float;
                _master._UpdateToAdapter();
                _master.UpdateAndExecuteOnInteraction_ExecuteMaster();
                return;
            }
            UpdateAndExecuteOnInteraction_ExecuteMaster();
        }

        public void UpdateAndExecuteOnInteraction_ExecuteMaster()
        {
            Serialize();
            _UpdateValues();
            ExecuteEvents();
            SyncRemotesIfHiveAndMaster();
        }

        private void SyncRemotesIfHiveAndMaster()
        {
            //Sync to remote actions
            if (hiveType == HIVE_MASTER)
            {
                foreach (ThryAction a in _remotes)
                {
                    a.SyncValuesFromMaster();
                }
            }
        }

        public void SyncValuesFromMaster()
        {
            prev_local_bool = _master.prev_local_bool;
            local_float = _master.local_float;
            local_bool = _master.local_bool;
            _UpdateToAdapter();
            _UpdateValues();
        }

        private void ExecuteEvents()
        {
            ExecuteAnimatorsTrigger();
            ExecuteUdonEvents();

            if (teleportTarget != null) Networking.LocalPlayer.TeleportTo(teleportTarget.position, teleportTarget.rotation);
        }

        private void ExecuteUdonEvents()
        {
            for(int i = 0; i < udon_event_Behvaiours.Length; i++)
            {
                ((UdonBehaviour)udon_event_Behvaiours[i].GetComponent(typeof(UdonBehaviour))).SendCustomEvent(udon_event_Names[i]);
            }
        }

        private void ExecuteAnimatorsTrigger()
        {
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animatorParameterNames[i].Length > 0)
                {
                    if (animatorParameterTypes[i] == (int)UnityEngine.AnimatorControllerParameterType.Trigger) animators[i].SetTrigger(animatorParameterNames[i]);
                }
            }
        }

        //===========Update Values===========
        private void _UpdateValues()
        {
            if (actionType == ACTION_TYPE_EVENT)
            {
                if (prev_local_bool != local_bool)
                {
                    UpdateEventToggles();
                }
            }
            if (actionType == ACTION_TYPE_BOOL)
            {
                UpdateBoolAnimators();
                UpdateBoolToggles();
            }
            else if(actionType == ACTION_TYPE_FLOAT)
            {
                UpdateFloatAnimators();
                UpdateBoolToggles();
            }
            UpdateUdonValues();
        }

        private void UpdateBoolAnimators()
        {
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animatorParameterNames[i].Length > 0)
                {
                    if (animatorParameterTypes[i] == (int)UnityEngine.AnimatorControllerParameterType.Bool) animators[i].SetBool(animatorParameterNames[i], local_bool);
                }
            }
        }

        private void UpdateFloatAnimators()
        {
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animatorParameterNames[i].Length > 0)
                {
                    if (animatorParameterTypes[i] == (int)UnityEngine.AnimatorControllerParameterType.Float) animators[i].SetFloat(animatorParameterNames[i], local_float);
                    else if (animatorParameterTypes[i] == (int)UnityEngine.AnimatorControllerParameterType.Int) animators[i].SetInteger(animatorParameterNames[i], (int)local_float);
                }
            }
        }

        private void UpdateBoolToggles()
        {
            foreach (GameObject o in toggleObjects) o.SetActive(local_bool);
            foreach (GameObject o in toggleObjectsInverted) o.SetActive(!local_bool);
            foreach (Collider c in toggleColliders) c.enabled = local_bool;
            foreach (VRC_Pickup p in togglePickups) p.pickupable = local_bool;
        }

        private void UpdateEventToggles()
        {
            foreach (GameObject o in toggleObjects) o.SetActive(!o.activeSelf);
            foreach (Collider c in toggleColliders) c.enabled = !c.enabled;
            foreach (VRC_Pickup p in togglePickups) p.pickupable = !p.pickupable;
        }

        private void UpdateUdonValues()
        {
            for (int i = 0; i < udon_bool_Behvaiours.Length; i++)
            {
                ((UdonBehaviour)udon_bool_Behvaiours[i].GetComponent(typeof(UdonBehaviour))).SetProgramVariable(udon_bool_Names[i], local_bool);
            }
            for (int i = 0; i < udon_float_Behvaiours.Length; i++)
            {
                ((UdonBehaviour)udon_float_Behvaiours[i].GetComponent(typeof(UdonBehaviour))).SetProgramVariable(udon_float_Names[i], local_float);
            }
            for (int i = 0; i < udon_int_Behvaiours.Length; i++)
            {
                ((UdonBehaviour)udon_int_Behvaiours[i].GetComponent(typeof(UdonBehaviour))).SetProgramVariable(udon_int_Names[i], (int)local_float);
            }
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR

    [CustomEditor(typeof(ThryAction))]
    public class ThryActionEditor : Editor
    {
        enum ActionType { Event, Bool, Float}
        enum SpecialBehaviourType { Normal, MirrorManager }
        enum HiveType { None, Master, Remote }


        bool headerClapper;
        bool headerAct;
        bool headerReq;

        ThryAction action;

        bool showClapperGUI;

        bool hasAdapter;

        bool isNotInit = true;
        GUIStyle headerStyle;

        void Init()
        {
            headerStyle = new GUIStyle("ShurikenModuleTitle")
            {
                border = new RectOffset(15, 7, 4, 4),
                fixedHeight = 22,
                contentOffset = new Vector2(20f, -2f)
            };

            action = (ThryAction)target;

            AutoAddComponents();

            Transform t = action.transform.parent;
            while(t != null)
            {
                if (t.GetComponent<Thry.Clapper.Clapper>() != null) showClapperGUI = true;
                t = t.parent;
            }

            hasAdapter = action.gameObject.GetComponents<UdonBehaviour>().Where(u => u.programSource.name.ToLower().Contains("adapter")).Count() > 0;

            isNotInit = false;
        }

        UdonSharpBehaviour GetAdapter<T>()
        {
            UdonBehaviour adapter = action.GetComponents<UdonBehaviour>().Where(udon => udon.programSource!= null && udon.programSource.GetType() == typeof(UdonSharpProgramAsset) && 
                (udon.programSource as UdonSharpProgramAsset).sourceCsScript.GetClass() == typeof(T)).FirstOrDefault();
            if (adapter != null) return UdonSharpEditorUtility.GetProxyBehaviour(adapter);
            adapter = action.GetComponents<UdonBehaviour>().Where(udon => udon.programSource == null).FirstOrDefault();
            if (adapter == null)
            {
                adapter = action.gameObject.AddComponent<UdonBehaviour>();
            }
            string[] guids = AssetDatabase.FindAssets(typeof(T).Name+ " t:UdonSharpProgramAsset");
            if (guids.Length > 0)
            {
                UdonSharpProgramAsset udonProgram = (UdonSharpProgramAsset)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (udonProgram != null && udonProgram.GetSerializedUdonProgramAsset() != null)
                {
                    adapter.AssignProgramAndVariables(udonProgram.GetSerializedUdonProgramAsset(), new VRC.Udon.Common.UdonVariableTable());
                    adapter.programSource = udonProgram;
                    UdonSharpEditorUtility.GetProxyBehaviour(adapter);
                }
            }
            return UdonSharpEditorUtility.GetProxyBehaviour(adapter);
        }

        void AutoAddComponents()
        {
            MonoBehaviour uiObjToAddCall = null;
            SerializedObject serialUIObj = null;
            if (action.GetComponent<Slider>() != null)
            {
                UI_Slider_Adapter adapter = (UI_Slider_Adapter)GetAdapter<UI_Slider_Adapter>();

                uiObjToAddCall = action.GetComponent<Slider>();
                serialUIObj = new SerializedObject(uiObjToAddCall);

                action.actionType = (int)ActionType.Float;

                adapter._uiSlider = action.GetComponent<Slider>();
                UdonSharpEditorUtility.CopyProxyToUdon(adapter);
            }
            else if (action.GetComponent<Toggle>() != null)
            {
                UI_Toggle_Adapter adapter = (UI_Toggle_Adapter)GetAdapter<UI_Toggle_Adapter>();

                uiObjToAddCall = action.GetComponent<Toggle>();
                serialUIObj = new SerializedObject(uiObjToAddCall);

                action.actionType = (int)ActionType.Bool;

                adapter._uiToggle = action.GetComponent<Toggle>();
                UdonSharpEditorUtility.CopyProxyToUdon(adapter);
            }
            else if(action.GetComponent<Button>() != null)
            {
                action.actionType = 0;

                uiObjToAddCall = action.GetComponent<Button>();
                serialUIObj = new SerializedObject(uiObjToAddCall);
            }

            foreach (UdonBehaviour u in action.GetComponents<UdonBehaviour>())
                u.SyncMethod = Networking.SyncType.Manual;

            EditorUtility.SetDirty(target);

            //Add call
            if (serialUIObj != null)
            {
                bool hasCall = false;

                SerializedProperty serialPropCalls = null;
                if      (uiObjToAddCall.GetType() == typeof(Slider)) serialPropCalls = serialUIObj.FindProperty("m_OnValueChanged.m_PersistentCalls.m_Calls");
                else if (uiObjToAddCall.GetType() == typeof(Toggle)) serialPropCalls = serialUIObj.FindProperty("onValueChanged.m_PersistentCalls.m_Calls");
                else if (uiObjToAddCall.GetType() == typeof(Button)) serialPropCalls = serialUIObj.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");

                if (serialPropCalls != null)
                {
                    for (int i = 0; i < serialPropCalls.arraySize; i++)
                    {
                        SerializedProperty item = serialPropCalls.GetArrayElementAtIndex(i);
                        if (item.FindPropertyRelative("m_Target").objectReferenceValue == UdonSharpEditorUtility.GetBackingUdonBehaviour(action)
                            && item.FindPropertyRelative("m_MethodName").stringValue == nameof(UdonBehaviour.SendCustomEvent)
                            && item.FindPropertyRelative("m_Arguments") != null
                            && item.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue == nameof(action.OnInteraction))
                            hasCall = true;
                    }
                }
                if (!hasCall)
                {
                    UnityAction<string> methodDelegate = UnityAction.CreateDelegate(typeof(UnityAction<string>), UdonSharpEditorUtility.GetBackingUdonBehaviour(action), typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.SendCustomEvent))) as UnityAction<string>;
                    if      (uiObjToAddCall.GetType() == typeof(Slider)) UnityEventTools.AddStringPersistentListener(((Slider)uiObjToAddCall).onValueChanged, methodDelegate, nameof(action.OnInteraction));
                    else if (uiObjToAddCall.GetType() == typeof(Toggle)) UnityEventTools.AddStringPersistentListener(((Toggle)uiObjToAddCall).onValueChanged, methodDelegate, nameof(action.OnInteraction));
                    else if (uiObjToAddCall.GetType() == typeof(Button)) UnityEventTools.AddStringPersistentListener(((Button)uiObjToAddCall).onClick, methodDelegate, nameof(action.OnInteraction));
                }
            }
        }

        bool synced;
        SpecialBehaviourType behaviourType;
        ActionType actionType;
        HiveType hiveType;

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();

            action = (ThryAction)target;

            if (isNotInit) Init();

            EditorGUILayout.LabelField("<size=30><color=#f542da>Thry's Action Script</color></size>", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter }, GUILayout.Height(50));

            //____________Behaviour__________
            EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            behaviourType = (SpecialBehaviourType)EditorGUILayout.EnumPopup("Special Behaviour", (SpecialBehaviourType)action.specialActionType);
            if(behaviourType == SpecialBehaviourType.Normal)
                actionType = (ActionType)EditorGUILayout.EnumPopup("Type", (ActionType)action.actionType);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(action, "Modify Type");
                action.actionType = (int)actionType;
                action.specialActionType = (int)behaviourType;
            }

            if (!hasAdapter && behaviourType == SpecialBehaviourType.Normal)
            {
                if (actionType == ActionType.Bool) action.local_bool = EditorGUILayout.Toggle("Boolean", action.local_bool);
                else if (actionType == ActionType.Float) action.local_float = EditorGUILayout.FloatField("Float", action.local_float);
            }

            if(showClapperGUI)
            {
                ClapperGUI();
            }

            GUISyncing();
            GUIHive();

            //__________Other UI____________
            if (behaviourType == SpecialBehaviourType.MirrorManager)
            {
                MirrorMangerGUI();
            }
            else
            {
                NormalGUI();
            }

            UdonSharpEditorUtility.CopyProxyToUdon(action);
            serializedObject.ApplyModifiedProperties();

        }

        bool headerSync;
        private void GUISyncing()
        {
            if (hiveType != HiveType.Remote && behaviourType != SpecialBehaviourType.MirrorManager)
            {
                headerSync = EditorGUILayout.BeginFoldoutHeaderGroup(headerSync, "Syncing", headerStyle);
                if (headerSync)
                {
                    EditorGUI.BeginChangeCheck();

                    synced = EditorGUILayout.Toggle("Is Synced", action.is_synced);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(action, "Modify Syncing");
                        action.is_synced = synced;
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            else
            {
                action.is_synced = false;
            }
        }

        bool headerHive;
        private void GUIHive()
        {
            headerHive = EditorGUILayout.BeginFoldoutHeaderGroup(headerHive, "Hive Control", headerStyle);
            if (headerHive)
            {
                EditorGUILayout.LabelField("Hive", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();

                hiveType = (HiveType)EditorGUILayout.EnumPopup("Hive Type", (HiveType)action.hiveType);
                if (hiveType == HiveType.Remote) action._master = (ThryAction)EditorGUILayout.ObjectField("Master Action", action._master, typeof(ThryAction), true);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(action, "Modify Hive");
                    action.hiveType = (int)hiveType;
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void ClapperGUI()
        {
            headerClapper = EditorGUILayout.BeginFoldoutHeaderGroup(headerClapper, "Clapper Settings", headerStyle);
            if (headerClapper)
            {
                EditorGUI.indentLevel += 1;
                action.isClapperAction = EditorGUILayout.Toggle("Is Clapper Action", action.isClapperAction);
                action.requiredClaps = EditorGUILayout.IntField("Required Claps", action.requiredClaps);
                action.desktopKey = EditorGUILayout.TextField("Desktop Key", action.desktopKey);
                EditorGUI.indentLevel -= 1;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void ArrayGUI(string name, string text, string tooltip = "")
        {
            var property = serializedObject.FindProperty(name);
            EditorGUILayout.PropertyField(property, new GUIContent(text, tooltip));
        }

        struct ArrayData
        {
            public string title;
            public string tooltip;
            public UnityEngine.Object[] unityA;
            public int[] intA;
            public float[] floatA;
            public string[] stringA;
            public int type;
            public Type enumType;

            public ArrayData(string title, string tooltip, UnityEngine.Object[] a, Type t)
            {
                this.title = title;
                this.tooltip = tooltip;
                this.type = 0;
                this.unityA = a == null ? (UnityEngine.Object[])Array.CreateInstance(t, 0) : a;
                this.intA = null;
                this.floatA = null;
                this.stringA = null;
                this.enumType = null;
            }

            public ArrayData(string title, string tooltip, int[] a)
            {
                this.title = title;
                this.tooltip = tooltip;
                this.type = 1;
                this.unityA = null;
                this.intA = a == null ? new int[0] : a;
                this.floatA = null;
                this.stringA = null;
                this.enumType = null;
            }

            public ArrayData(string title, string tooltip, int[] a, Type enumType)
            {
                this.title = title;
                this.tooltip = tooltip;
                this.type = 1;
                this.unityA = null;
                this.intA = a == null ? new int[0] : a;
                this.floatA = null;
                this.stringA = null;
                this.enumType = enumType;
            }

            public ArrayData(string title, string tooltip, float[] a)
            {
                this.title = title;
                this.tooltip = tooltip;
                this.type = 2;
                this.unityA = null;
                this.intA = null;
                this.floatA = a == null ? new float[0] : a;
                this.stringA = null;
                this.enumType = null;
            }

            public ArrayData(string title, string tooltip, string[] a)
            {
                this.title = title;
                this.tooltip = tooltip;
                this.type = 3;
                this.unityA = null;
                this.intA = null;
                this.floatA = null;
                this.stringA = a==null?new string[0]:a;
                this.enumType = null;
            }

            public int Length()
            {
                if (type == 0) return unityA.Length;
                if (type == 1) return intA.Length;
                if (type == 2) return floatA.Length;
                if (type == 3) return stringA.Length;
                return 0;
            }

            public void NewLength(int l)
            {
                if(type == 0)
                {
                    UnityEngine.Object[] old = unityA;
                    unityA = (UnityEngine.Object[])Array.CreateInstance(unityA.GetType().GetElementType(), l);
                    Array.Copy(old, unityA, Math.Min(l, old.Length));
                }else if (type == 1)
                {
                    int[] old = intA;
                    intA = new int[l];
                    Array.Copy(old, intA, Math.Min(l, old.Length));
                }else if (type == 2)
                {
                    float[] old = floatA;
                    floatA = new float[l];
                    Array.Copy(old, floatA, Math.Min(l, old.Length));
                }else if (type == 3)
                {
                    string[] old = stringA;
                    stringA = new string[l];
                    Array.Copy(old, stringA, Math.Min(l, old.Length));
                }
            }

            public Enum GetEnumValue(int index)
            {
                int i = intA[index];
                if(Enum.IsDefined(enumType, i) == false)
                {
                    i = 0;
                }
                return (Enum)Enum.ToObject(enumType, i);
            }
        }

        private ArrayData[] ArraysGUI(params ArrayData[] arrays)
        {
            EditorGUI.BeginChangeCheck();
            int length = EditorGUILayout.IntField(arrays[0].Length());

            if(EditorGUI.EndChangeCheck() || arrays.Any(a => a.Length() < length)){
                for (int i = 0; i < arrays.Length; i++)
                {
                    arrays[i].NewLength(length);
                }
            }
            Rect headerR = EditorGUILayout.GetControlRect(true);
            headerR.width = headerR.width / arrays.Length;
            for (int a = 0; a < arrays.Length; a++)
            {
                EditorGUI.LabelField(headerR, new GUIContent(arrays[a].title, arrays[a].tooltip), EditorStyles.boldLabel);
                headerR.x += headerR.width;
            }
            for (int i = 0; i < length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int a = 0; a < arrays.Length; a++)
                {
                    if (arrays[a].type == 0) arrays[a].unityA[i] = EditorGUILayout.ObjectField(arrays[a].unityA[i], arrays[a].unityA.GetType().GetElementType(), true);
                    else if (arrays[a].type == 1 && arrays[a].enumType != null) arrays[a].intA[i] = Convert.ToInt32(EditorGUILayout.EnumPopup(arrays[a].GetEnumValue(i)));
                    else if (arrays[a].type == 1) arrays[a].intA[i] = EditorGUILayout.IntField(arrays[a].intA[i]);
                    else if (arrays[a].type == 2) arrays[a].floatA[i] = EditorGUILayout.FloatField(arrays[a].floatA[i]);
                    else if (arrays[a].type == 3) arrays[a].stringA[i] = EditorGUILayout.TextField(arrays[a].stringA[i]);
                    else EditorGUILayout.LabelField("Missing Type");
                }
                EditorGUILayout.EndHorizontal();
            }
            return arrays;
        }

        private void NormalGUI()
        {
            headerAct = EditorGUILayout.BeginFoldoutHeaderGroup(headerAct, "Actions", headerStyle);
            if (headerAct)
            {
                EditorGUI.indentLevel += 1;
                ArrayGUI(nameof(action.toggleObjects), "Toggle GameObjects");
                if(actionType == ActionType.Bool) ArrayGUI(nameof(action.toggleObjectsInverted), "Toggle GameObjects Inverted");
                ArrayGUI(nameof(action.toggleColliders), "Toggle Colliders");
                ArrayGUI(nameof(action.togglePickups), "Toggle Pickups");
                action.teleportTarget = (Transform)EditorGUILayout.ObjectField(new GUIContent("Teleport to"), action.teleportTarget, typeof(Transform), true);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Udon Calls", EditorStyles.boldLabel);
                if (actionType == ActionType.Bool|| actionType == ActionType.Float)
                {
                    ArrayData[] arrays = ArraysGUI(
                        new ArrayData("Udon Behaviour", "", action.udonBehaviours, typeof(GameObject)),
                        new ArrayData("Event Name", "Event to be called", action.udonEventNames),
                        new ArrayData("Value Name", "This variable will be set to the value of the ui element", action.udonValueNames));
                    action.udonBehaviours = (GameObject[])arrays[0].unityA;
                    action.udonEventNames = arrays[1].stringA;
                    action.udonValueNames = arrays[2].stringA;
                }
                else
                {
                    ArrayData[] arrays = ArraysGUI(
                        new ArrayData("Udon Behaviour", "", action.udonBehaviours, typeof(GameObject)),
                        new ArrayData("Event Name", "Event to be called", action.udonEventNames));
                    action.udonBehaviours = (GameObject[])arrays[0].unityA;
                    action.udonEventNames = arrays[1].stringA;
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Animators", EditorStyles.boldLabel);
                ArrayData[] arraysAnimator = ArraysGUI(
                    new ArrayData("Animator", "", action.animators, typeof(Animator)),
                    new ArrayData("Parameter Type", "", action.animatorParameterTypes, typeof(UnityEngine.AnimatorControllerParameterType)),
                    new ArrayData("Parameter Name", "Event to be called", action.animatorParameterNames));
                action.animators = (Animator[])arraysAnimator[0].unityA;
                action.animatorParameterTypes = arraysAnimator[1].intA;
                action.animatorParameterNames = arraysAnimator[2].stringA;
                EditorGUILayout.Space();
                EditorGUI.indentLevel -= 1;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            headerReq = EditorGUILayout.BeginFoldoutHeaderGroup(headerReq, "Requirements", headerStyle);
            if (headerReq)
            {
                EditorGUI.indentLevel += 1;
                ArrayGUI(nameof(action.hasToBeInsideAnyCollider), "Be Inside Collider", "Player has to be in one of these colliders for action to be executed.");
                EditorGUILayout.Space();
                action.hasToBeInstanceOwner = EditorGUILayout.Toggle(new GUIContent("Has to be Instance Owner"), action.hasToBeInstanceOwner);
                action.hasToBeOwner = EditorGUILayout.Toggle(new GUIContent("Has to be GameObject Owner"), action.hasToBeOwner);
                action.hasToBeMaster = EditorGUILayout.Toggle(new GUIContent("Has to be Master"), action.hasToBeMaster);
                if (action.hasToBeOwner) action.hasToBeOwnerGameobject = (GameObject)EditorGUILayout.ObjectField(new GUIContent("GameObject to own"), action.hasToBeOwnerGameobject, typeof(GameObject), true);
                EditorGUILayout.Space();
                ArrayGUI(nameof(action.autherizedPlayerDisplayNames), "Autherized Players", "Player display name has to match one of this list.");
                EditorGUI.indentLevel -= 1;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        bool headerMirror;
        private void MirrorMangerGUI()
        {
            headerMirror = EditorGUILayout.BeginFoldoutHeaderGroup(headerMirror, "Mirror Manager", headerStyle);
            if (headerMirror)
            {
                EditorGUI.indentLevel += 1;
                ArrayGUI(nameof(action.mirrors), "Mirrors", "Nees to be the actual mirror plane.");
                action.maximumOpenDistance = EditorGUILayout.FloatField(new GUIContent("Maximum Distance", "Maximum distance the player can stand from the mirror and open it."), action.maximumOpenDistance);
                EditorGUI.indentLevel -= 1;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }

#endif
}