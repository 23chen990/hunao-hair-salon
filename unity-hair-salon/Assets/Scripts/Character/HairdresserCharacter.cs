using System;
using UnityEngine;

namespace HairSalon.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class HairdresserCharacter : MonoBehaviour, IHairdresserVisualReplacer
    {
        private static readonly int StateParameter = Animator.StringToHash("State");
        private static readonly int DirectionParameter = Animator.StringToHash("Direction");

        [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
        [SerializeField, Min(0f)] private float turnResponsiveness = 12f;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform rightHandToolSocket;

        private Animator _animator;
        private Hairdresser2DPresenter _presenter;
        private Hairdresser2_5DRig _twoPointFiveDRig;
        private bool _serviceActive;

        public HairdresserAnimationState CurrentState { get; private set; } = HairdresserAnimationState.Idle;
        public HairdresserDirection FacingDirection { get; private set; } = HairdresserDirection.South;
        public Transform VisualRoot { get { ResolveBindings(); return visualRoot; } }
        public Transform RightHandToolSocket { get { ResolveBindings(); return rightHandToolSocket; } }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _twoPointFiveDRig = GetComponent<Hairdresser2_5DRig>();
            ResolveBindings();
            ApplyAnimatorParameters();
        }

        public void Move(Vector3 worldDirection, float deltaTime)
        {
            Vector3 planar = new Vector3(worldDirection.x, 0f, worldDirection.z);
            if (planar.sqrMagnitude <= 0.0001f)
            {
                if (!_serviceActive) SetState(HairdresserAnimationState.Idle);
                return;
            }

            Vector3 normalized = planar.normalized;
            transform.position += normalized * (moveSpeed * Mathf.Max(0f, deltaTime));
            Face(normalized, deltaTime);
            if (!_serviceActive) SetState(HairdresserAnimationState.Walk);
        }

        public void MoveTowards(Vector3 worldTarget, float deltaTime)
        {
            Vector3 target = new Vector3(worldTarget.x, transform.position.y, worldTarget.z);
            Vector3 delta = target - transform.position;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                transform.position = target;
                Move(Vector3.zero, deltaTime);
                return;
            }

            Vector3 direction = delta.normalized;
            transform.position = Vector3.MoveTowards(
                transform.position, target, moveSpeed * Mathf.Max(0f, deltaTime));
            Face(direction, deltaTime);
            if (!_serviceActive) SetState(HairdresserAnimationState.Walk);
            if ((target - transform.position).sqrMagnitude <= 0.0001f && !_serviceActive)
                SetState(HairdresserAnimationState.Idle);
        }

        public void FaceTowards(Vector3 worldDirection)
        {
            Vector3 planar = new Vector3(worldDirection.x, 0f, worldDirection.z);
            if (planar.sqrMagnitude <= .0001f) return;
            Vector3 direction = planar.normalized;
            FacingDirection = _twoPointFiveDRig == null
                ? HairdresserDirectionResolver.FromVector(new Vector2(direction.x, direction.z))
                : _twoPointFiveDRig.ResolveDirection(direction);
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (_animator != null) _animator.SetInteger(DirectionParameter, (int)FacingDirection);
            ApplyPresentation();
        }

        public void BeginService(HairdresserAnimationState serviceState)
        {
            if (serviceState != HairdresserAnimationState.CutHair &&
                serviceState != HairdresserAnimationState.DryHair &&
                serviceState != HairdresserAnimationState.WashHair)
            {
                throw new ArgumentOutOfRangeException(nameof(serviceState), serviceState,
                    "Only reserved service states can be started.");
            }
            _serviceActive = true;
            SetState(serviceState);
        }

        public void EndService()
        {
            _serviceActive = false;
            SetState(HairdresserAnimationState.Idle);
        }

        public void AttachTool(Transform tool)
        {
            if (tool == null) return;
            ResolveBindings();
            if (rightHandToolSocket == null) return;
            tool.SetParent(rightHandToolSocket, false);
            tool.localPosition = Vector3.zero;
            tool.localRotation = Quaternion.identity;
        }

        public GameObject ReplaceVisual(GameObject visualPrefab)
        {
            if (visualPrefab == null) throw new ArgumentNullException(nameof(visualPrefab));
            ResolveBindings();
            if (visualRoot == null)
                throw new MissingReferenceException("Hairdresser Visual slot is missing.");

            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = visualRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            GameObject replacement = Instantiate(visualPrefab, visualRoot, false);
            replacement.name = visualPrefab.name;
            _presenter = replacement.GetComponentInChildren<Hairdresser2DPresenter>();
            ApplyPresentation();
            return replacement;
        }

        private void Face(Vector3 direction, float deltaTime)
        {
            FacingDirection = _twoPointFiveDRig == null
                ? HairdresserDirectionResolver.FromVector(new Vector2(direction.x, direction.z))
                : _twoPointFiveDRig.ResolveDirection(direction);
            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            float blend = 1f - Mathf.Exp(-turnResponsiveness * Mathf.Max(0f, deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, target, blend);
            if (_animator != null) _animator.SetInteger(DirectionParameter, (int)FacingDirection);
            ApplyPresentation();
        }

        private void SetState(HairdresserAnimationState state)
        {
            CurrentState = state;
            if (_animator != null) _animator.SetInteger(StateParameter, (int)state);
            ApplyPresentation();
        }

        private void ApplyAnimatorParameters()
        {
            if (_animator == null) return;
            _animator.applyRootMotion = false;
            _animator.SetInteger(StateParameter, (int)CurrentState);
            _animator.SetInteger(DirectionParameter, (int)FacingDirection);
            ApplyPresentation();
        }

        private void ResolveBindings()
        {
            if (visualRoot == null) visualRoot = transform.Find("Visual");
            if (rightHandToolSocket == null)
                rightHandToolSocket = transform.Find("Sockets/RightHandToolSocket");
            if (_presenter == null && visualRoot != null)
                _presenter = visualRoot.GetComponentInChildren<Hairdresser2DPresenter>();
            if (_twoPointFiveDRig == null) _twoPointFiveDRig = GetComponent<Hairdresser2_5DRig>();
        }

        private void ApplyPresentation()
        {
            if (_presenter != null) _presenter.Apply(CurrentState, FacingDirection);
        }
    }
}
