using UnityEngine;

namespace HairSalon.Character
{
    [DisallowMultipleComponent]
    public sealed class Hairdresser2_5DRig : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer contactShadow;
        [SerializeField] private bool cameraRelativeDirections = true;

        public SpriteRenderer ContactShadow
        {
            get
            {
                ResolveBindings();
                return contactShadow;
            }
        }

        private void Awake() => ResolveBindings();

        public HairdresserDirection ResolveDirection(Vector3 worldDirection)
        {
            Camera sceneCamera = Camera.main;
            if (!cameraRelativeDirections || sceneCamera == null)
                return HairdresserDirectionResolver.FromVector(
                    new Vector2(worldDirection.x, worldDirection.z));

            return ResolveCameraRelativeDirection(
                worldDirection, sceneCamera.transform.right, sceneCamera.transform.forward);
        }

        public static HairdresserDirection ResolveCameraRelativeDirection(
            Vector3 worldDirection, Vector3 cameraRight, Vector3 cameraForward)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            Vector3 planarRight = Vector3.ProjectOnPlane(cameraRight, Vector3.up).normalized;
            Vector3 planarForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
            if (planarDirection.sqrMagnitude <= .0001f ||
                planarRight.sqrMagnitude <= .0001f || planarForward.sqrMagnitude <= .0001f)
            {
                return HairdresserDirectionResolver.FromVector(
                    new Vector2(worldDirection.x, worldDirection.z));
            }

            Vector2 viewDirection = new Vector2(
                Vector3.Dot(planarDirection, planarRight),
                Vector3.Dot(planarDirection, planarForward));
            return HairdresserDirectionResolver.FromVector(viewDirection);
        }

        private void ResolveBindings()
        {
            if (visualRoot == null) visualRoot = transform.Find("Visual");
            if (contactShadow == null)
            {
                Transform shadow = transform.Find("Grounding/ContactShadow");
                if (shadow != null) contactShadow = shadow.GetComponent<SpriteRenderer>();
            }
        }
    }
}
