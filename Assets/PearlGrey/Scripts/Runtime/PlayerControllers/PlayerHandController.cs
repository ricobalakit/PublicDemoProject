﻿using System.Collections;
using UnityEngine;

namespace PearlGreySoftware
{

    public class PlayerHandController : PlayerBodyPartController
    {
        // TODO-RPB: Spin off the animation functionality into a PlayerHandAnimationController or something like that
        // TODO-RPB: Spin off the face button and other event things into, for example, a SpaceShooterPlayerHandController class that inherits from this
        // TODO-RPB: The above steps would help make this a more generalized template to use for other VR apps/games
        // TODO-RPB: Add haptics

        #region Public Enums

        public enum HandSide
        {
            Unassigned = 0,
            Left = 1,
            Right = 2
        }

        #endregion

        #region Public Events

        public HandVoidEvent OnTriggerDown = new HandVoidEvent();
        public HandVoidEvent OnTriggerUp = new HandVoidEvent();
        public HandVoidEvent OnFaceButtonDown = new HandVoidEvent();

        #endregion

        #region Public Properties

        public HandSide Chirality
        {
            get
            {
                // TODO-RPB: This is lazy init, which isn't great :/
                if (m_chirality == HandSide.Unassigned)
                {
                    SetChiralityFromTrackedPoseDriver();
                }

                return m_chirality;
            }
        }

        #endregion

        #region Private Fields

        [ReadOnly]
        [SerializeField]
        private HandSide m_chirality = default;

        [SerializeField]
        private Transform m_gripPivot = null;

        [SerializeField]
        private Transform m_indexPivot = null;

        private InputName m_gripInputName = InputName.None;
        private InputName m_indexInputName = InputName.None;
        private InputName m_faceFrontButtonName = InputName.None;
        private InputName m_faceBackButtonName = InputName.None;

        private InteractiveObject m_lastSuccessfulHighlightedInteractiveObject = null;
        private InteractiveObject m_currentInteractingOnject = null; // RPB: For now, keep it simple and only consider one object interactible at a time.

        #endregion

        #region Protected Methods

        protected new void Start()
        {
            base.Start();
            SetChiralityFromTrackedPoseDriver();
            StartCoroutine(Initialize());
        }

        #endregion

        #region Private Methods

        private IEnumerator Initialize()
        {
            SetStatus(StandardStatus.INITIALIZATION_RUNNING);
            SetStatus(StandardStatus.INITIALIZATION_WAITING);

            yield return new WaitWhile(() => GameManager.Instance == null);
            yield return new WaitWhile(() => GameManager.Instance.InputManager == null);
            yield return new WaitWhile(() => !GameManager.Instance.InputManager.IsInitialized);

            SetStatus(StandardStatus.INITIALIZATION_RUNNING);

            if (m_chirality == HandSide.Left)
            {
                m_gripInputName = InputName.GripLeftAxis;
                m_indexInputName = InputName.IndexLeftAxis;
                m_faceFrontButtonName = InputName.FaceFrontLeftButton;
                m_faceBackButtonName = InputName.FaceBackLeftButton;
            }
            else if (m_chirality == HandSide.Right)
            {
                m_gripInputName = InputName.GripRightAxis;
                m_indexInputName = InputName.IndexRightAxis;
                m_faceFrontButtonName = InputName.FaceFrontRightButton;
                m_faceBackButtonName = InputName.FaceBackRightButton;
            }
            else
            {
                SetStatus(StandardStatus.INITIALIZATION_STOPPED_ERROR);
                yield break;
            }

            var inputStates = GameManager.Instance.InputManager.InputStates;
            inputStates[m_gripInputName].OnValueChanged.AddListener(OnGripValueChanged);
            inputStates[m_gripInputName].OnInputUp.AddListener(OnGripUp);
            inputStates[m_gripInputName].OnInputDown.AddListener(OnGripDown);
            inputStates[m_indexInputName].OnValueChanged.AddListener(OnIndexValueChanged);
            inputStates[m_indexInputName].OnInputUp.AddListener(OnIndexUp);
            inputStates[m_indexInputName].OnInputDown.AddListener(OnIndexDown);
            inputStates[m_faceFrontButtonName].OnInputDown.AddListener(OnAnyFaceButtonDown);
            inputStates[m_faceBackButtonName].OnInputDown.AddListener(OnAnyFaceButtonDown);

            SetInitialized($"Initialization as {m_chirality} hand finished");
        }

        private void SetChiralityFromTrackedPoseDriver()
        {
            if (m_trackedPoseDriver.poseSource == UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.LeftPose)
            {
                m_chirality = HandSide.Left;
                Log("Chirality Set: Left");
            }
            else if (m_trackedPoseDriver.poseSource == UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.RightPose)
            {
                m_chirality = HandSide.Right;
                Log("Chirality Set: Right");
            }
            else
            {
                Debug.LogError($"Could not set chirality for {gameObject.name}");
                Log("Couldn't set chirality", LogType.Error);
            }

        }

        private void OnGripValueChanged(float newValue)
        {
            UpdateGripVisuals(newValue);
        }

        private void OnGripDown()
        {
            if (m_lastSuccessfulHighlightedInteractiveObject != null && m_currentInteractingOnject == null)
            {
                if (m_lastSuccessfulHighlightedInteractiveObject.TryStartInteraction(this))
                {
                    m_currentInteractingOnject = m_lastSuccessfulHighlightedInteractiveObject;
                }
            }
        }

        private void OnGripUp()
        {
            if (m_currentInteractingOnject != null)
            {
                if (m_currentInteractingOnject.TryEndInteraction(this))
                {
                    m_currentInteractingOnject = null;
                }
            }
        }

        private void OnIndexValueChanged(float newValue)
        {
            UpdateIndexVisuals(newValue);
        }

        private void OnIndexDown()
        {
            OnTriggerDown.Invoke(this);
        }

        private void OnIndexUp()
        {
            OnTriggerUp.Invoke(this);
        }

        private void OnAnyFaceButtonDown()
        {
            OnFaceButtonDown.Invoke(this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var possibleInteractiveObject = collision.gameObject.GetComponent<InteractiveObject>();

            if (possibleInteractiveObject != null)
            {
                if (possibleInteractiveObject.TryStartHighlight(this))
                {
                    m_lastSuccessfulHighlightedInteractiveObject = possibleInteractiveObject;
                }
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            var possibleInteractiveObject = collision.gameObject.GetComponent<InteractiveObject>();

            if (possibleInteractiveObject != null)
            {
                possibleInteractiveObject.TryEndHighlight(this);

                if (m_lastSuccessfulHighlightedInteractiveObject == possibleInteractiveObject)
                {
                    m_lastSuccessfulHighlightedInteractiveObject = null;
                }
            }
        }

        private void UpdateGripVisuals(float newValue)
        {
            if (m_gripPivot == null)
            {
                Log($"{nameof(m_gripPivot)} null!", LogType.Error);
                return;
            }

            float yRotation = 0f;
            // TODO-RPB: Generalize these magic numbers
            if (m_chirality == HandSide.Left)
            {
                yRotation = Mathf.Lerp(-30f, 120f, newValue);
            }
            else if (m_chirality == HandSide.Right)
            {
                yRotation = Mathf.Lerp(30f, -120f, newValue);

            }

            m_gripPivot.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        }

        private void UpdateIndexVisuals(float newValue)
        {
            if (m_indexPivot == null)
            {
                Log($"{nameof(m_indexPivot)} null!", LogType.Error);
                return;
            }

            float yRotation = 0f;
            // TODO-RPB: Generalize these magic numbers
            if (m_chirality == HandSide.Left)
            {
                yRotation = Mathf.Lerp(-30f, 120f, newValue);
            }
            else if (m_chirality == HandSide.Right)
            {
                yRotation = Mathf.Lerp(30f, -120f, newValue);

            }

            m_indexPivot.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        }

        #endregion

    }
}