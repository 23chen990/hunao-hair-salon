using System;
using System.Collections;
using System.Collections.Generic;
using HairSalon;
using HairSalon.ServiceArchitecture;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    /// <summary>开发构建专用的真实资产候选场景；正式 Demo 场景不挂载此组件。</summary>
    public sealed class CandidateAssetValidation : MonoBehaviour
    {
        public const float AlignmentSettleSeconds = 4f;

        private SalonDemo _owner;
        private AssetManifest _manifest;
        private string _status = "候选资产准备中";
        private bool _debug;

        private IEnumerator Start()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
                throw new InvalidOperationException("CandidateAssetValidation may only run in a development build or the Editor.");
            _debug = QueryEnabled(Application.absoluteURL, "debug");
            yield return null;
            _owner = GetComponent<SalonDemo>() ?? FindAnyObjectByType<SalonDemo>();
            if (_owner == null) throw new MissingReferenceException("Candidate scene requires SalonDemo.");
            _manifest = AssetManifestLoader.LoadFromResources();
            AssetDefinition ordinary = FindCandidateByRole("ordinary");
            AssetDefinition serviceStation = FindCandidateByRole("service-station");
            RequireCandidate(ordinary, "ordinary");
            RequireCandidate(serviceStation, "service-station");
            BuildStandaloneCandidate(ordinary, ordinary.CandidateValidation, "Candidate Ordinary Asset");
            BindServiceStationCandidate(serviceStation, serviceStation.CandidateValidation);
            Debug.Log("[CANDIDATE_ASSETS_READY] A=" + ordinary.Id + " B=" + serviceStation.Id +
                      " orientation=" + serviceStation.DefaultOrientation + " mirrored=False rotated=False");
            StartCoroutine(RunExistingServiceRegression(
                serviceStation.CandidateValidation.StationId, serviceStation.DefaultOrientation));
        }

        private AssetDefinition FindCandidateByRole(string role)
        {
            return _manifest.Assets?.Find(asset => asset != null && IsCandidateStatus(asset.Status) &&
                asset.CandidateValidation != null && asset.CandidateValidation.Role == role);
        }

        private static void RequireCandidate(AssetDefinition asset, string role)
        {
            if (asset == null || !IsCandidateStatus(asset.Status))
                throw new InvalidOperationException("Candidate asset missing for role: " + role);
            if (asset.CandidateValidation == null || asset.CandidateValidation.Role != role)
                throw new InvalidOperationException("Candidate validation role failed: " + asset.Id);
            if (asset.Directions == null || asset.Directions.Count != 1 ||
                asset.Directions[0] != asset.DefaultOrientation)
                throw new InvalidOperationException("Candidate orientation contract failed: " + asset.Id);
            if (asset.Shadow == null || asset.Shadow.Mode != "baked" || asset.Shadow.UsesProceduralShadow)
                throw new InvalidOperationException("Candidate baked-shadow contract failed: " + asset.Id);
            if (role == "service-station" && asset.CandidateValidation.ServiceType != "wash")
                throw new InvalidOperationException("Candidate scene currently validates wash service stations only: " + asset.Id);
        }

        private static bool IsCandidateStatus(string status)
            => status == "candidate" || status == "NEEDS-REVIEW";

        private void BuildStandaloneCandidate(AssetDefinition asset, AssetCandidateValidation validation, string name)
        {
            Transform parent = GameObject.Find("Fixed Salon Map")?.transform;
            HideNamedVisualsNear(validation.WorldPosition, validation.HideRadius, validation.HideObjectNames);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = validation.WorldPosition;
            BuildArtwork(root, asset, asset.DefaultOrientation);
            AddCollider(root, asset);
            if (_debug) BuildDiagnostics(root, asset);
        }

        private void BindServiceStationCandidate(AssetDefinition asset, AssetCandidateValidation validation)
        {
            GameObject station = GameObject.Find(validation.TargetObjectName);
            if (station == null) throw new MissingReferenceException(validation.TargetObjectName + " is missing.");
            foreach (Renderer renderer in station.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (Collider collider in station.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            HideNamedVisualsNear(validation.WorldPosition, validation.HideRadius, validation.HideObjectNames);

            var visual = new GameObject("Candidate Visual [" + asset.DefaultOrientation + "]").transform;
            visual.SetParent(station.transform, false);
            visual.position = validation.WorldPosition;
            BuildArtwork(visual, asset, asset.DefaultOrientation);
            AddCollider(visual, asset);
            if (_debug) BuildDiagnostics(visual, asset);

            Vector3 origin = validation.WorldPosition;
            MoveExistingMarker(station.transform, "CustomerSeatAnchor", origin + Anchor(asset, "customer-seat").Position);
            MoveExistingMarker(station.transform, "PlayerServiceAnchor", origin + Anchor(asset, "stylist-work").Position);
            MoveExistingMarker(station.transform, "CustomerUIAnchor", origin + Anchor(asset, "service-vfx").Position);
            CreateMarker(station.transform, "QueueAnchor", origin + Anchor(asset, "queue").Position);
            CreateMarker(station.transform, "ToolAnchor", origin + Anchor(asset, "tool").Position);
            station.name = validation.TargetObjectName + " [Candidate " + asset.DefaultOrientation + "]";
        }

        private static AssetAnchor Anchor(AssetDefinition asset, string id)
        {
            AssetAnchor anchor = asset.InteractionAnchors?.Find(item => item != null && item.Id == id);
            if (anchor == null) throw new InvalidOperationException($"Candidate {asset.Id} is missing anchor {id}.");
            return anchor;
        }

        private static void MoveExistingMarker(Transform parent, string name, Vector3 position)
        {
            Transform marker = parent.Find(name);
            if (marker == null) throw new MissingReferenceException("Existing station marker is missing: " + name);
            marker.position = position;
        }

        private static void CreateMarker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.position = position;
        }

        public static void BuildArtwork(Transform parent, AssetDefinition asset, string orientation)
        {
            string resourcePath = asset.ResourceFor(orientation);
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) throw new MissingReferenceException("Candidate artwork is missing: " + resourcePath);
            float width = asset.WorldSize.x;
            float height = asset.WorldSize.y;
            var artwork = new GameObject("Artwork [unaltered " + orientation + "]", typeof(MeshFilter), typeof(MeshRenderer));
            artwork.name = "Artwork [unaltered " + orientation + "]";
            artwork.transform.SetParent(parent, false);
            artwork.transform.localScale = new Vector3(width, height, 1f);
            Camera sceneCamera = Camera.main;
            Vector3 forward = sceneCamera != null ? sceneCamera.transform.forward : Vector3.forward;
            artwork.transform.position = parent.position + forward * (asset.Sorting?.DepthOffset ?? 0f);
            if (sceneCamera != null) artwork.transform.rotation = sceneCamera.transform.rotation;
            var mesh = new Mesh { name = asset.Id + " Candidate Quad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-asset.Pivot.x, -asset.Pivot.y, 0f),
                new Vector3(1f - asset.Pivot.x, -asset.Pivot.y, 0f),
                new Vector3(-asset.Pivot.x, 1f - asset.Pivot.y, 0f),
                new Vector3(1f - asset.Pivot.x, 1f - asset.Pivot.y, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            artwork.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = artwork.GetComponent<MeshRenderer>();
            Shader shader = Resources.Load<Shader>("SalonCandidateArtwork");
            if (shader == null) throw new MissingReferenceException("SalonCandidateArtwork shader is missing.");
            var material = new Material(shader) { mainTexture = texture, color = Color.white, hideFlags = HideFlags.DontSave };
            material.SetFloat("_Cutoff", .001f);
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = string.IsNullOrWhiteSpace(asset.Sorting?.Layer) ? "Default" : asset.Sorting.Layer;
            renderer.sortingOrder = asset.Sorting?.Order ?? 0;
        }

        private static void HideNamedVisualsNear(Vector3 origin, float radius, List<string> names)
        {
            if (names == null || names.Count == 0) return;
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!names.Contains(renderer.gameObject.name)) continue;
                Vector2 delta = new Vector2(renderer.transform.position.x - origin.x, renderer.transform.position.z - origin.z);
                if (delta.magnitude > radius) continue;
                renderer.enabled = false;
                Collider collider = renderer.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }
        }

        private static void AddCollider(Transform root, AssetDefinition asset)
        {
            var collider = root.gameObject.AddComponent<BoxCollider>();
            float height = Mathf.Max(.5f, asset.WorldSize.y);
            collider.center = new Vector3(asset.Collision.Center.x, height * .5f, asset.Collision.Center.y);
            collider.size = new Vector3(asset.Collision.Size.x, height, asset.Collision.Size.y);
        }

        private static void BuildDiagnostics(Transform root, AssetDefinition asset)
        {
            Area(root, "Footprint Diagnostic", asset.Footprint, new Color(.05f, .65f, 1f, .28f), .025f);
            Area(root, "Collision Diagnostic", asset.Collision, new Color(1f, .15f, .15f, .34f), .055f);
            foreach (AssetAnchor anchor in asset.InteractionAnchors)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Anchor Diagnostic [" + anchor.Id + "]";
                marker.transform.SetParent(root, false);
                marker.transform.localPosition = anchor.Position;
                marker.transform.localScale = Vector3.one * .16f;
                marker.GetComponent<Collider>().enabled = false;
                Colorize(marker, new Color(.2f, 1f, .35f));
            }
        }

        private static void Area(Transform root, string name, AssetArea area, Color color, float y)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.SetParent(root, false);
            target.transform.localPosition = new Vector3(area.Center.x, y, area.Center.y);
            target.transform.localScale = new Vector3(area.Size.x, .04f, area.Size.y);
            target.GetComponent<Collider>().enabled = false;
            Colorize(target, color);
        }

        private static void Colorize(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            Shader shader = color.a < .999f ? Resources.Load<Shader>("SalonContactShadow") : Resources.Load<Shader>("SalonLowPoly");
            renderer.sharedMaterial = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        private IEnumerator RunExistingServiceRegression(int serviceStationId, string orientation)
        {
            _owner.RuntimeStartBusinessDay();
            yield return new WaitForSecondsRealtime(.25f);
            CustomerModel customer = _owner.RuntimeGame.Spawn(88201,
                new List<ServiceType> { ServiceType.Wash, ServiceType.Cut, ServiceType.Dry });
            if (customer == null) throw new InvalidOperationException("Candidate regression customer setup failed.");
            _owner.RuntimeAttachCustomerView(customer);
            _owner.RuntimeDay.Stats.RecordSpawn(customer);
            yield return new WaitForSecondsRealtime(SalonGameModel.EnteringSeconds + .2f);
            if (!_owner.RuntimeGame.Assign(customer, serviceStationId)) throw new InvalidOperationException("Candidate wash assignment failed.");
            yield return new WaitForSecondsRealtime(SalonGameModel.MovingToStationSeconds + .25f);
            _owner.RuntimeGame.SelectCustomer(customer);
            yield return new WaitForSecondsRealtime(AlignmentSettleSeconds);
            _status = "顾客与理发师已对齐候选洗发工位";
            Debug.Log("[CANDIDATE_ALIGNMENT_READY] customer=" + customer.Id + " station=" + serviceStationId +
                      " orientation=" + orientation);
            yield return new WaitForSecondsRealtime(3f);

            yield return RunWash(customer, WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration, false);
            yield return RunWash(customer, WashAction.Shampoo, _owner.RuntimeGame.ServiceConfig.ShampooDuration, true);
            yield return RunWash(customer, WashAction.Shower, _owner.RuntimeGame.ServiceConfig.RinseDuration, false);
            if (_owner.RuntimeGame.PerformQuickAction(customer, ActiveServiceAction.WrapTowel) != ServiceActionResult.QuickActionCompleted)
                throw new InvalidOperationException("Candidate towel step failed.");
            if (!PrepareHaircutTransfer(_owner.RuntimeGame, customer))
                throw new InvalidOperationException("Candidate haircut transfer preparation failed.");
            int haircutStationId = FindAvailableHaircutStation(_owner.RuntimeGame);
            if (haircutStationId < 0 || !_owner.RuntimeGame.Assign(customer, haircutStationId))
                throw new InvalidOperationException("Candidate transfer to haircut failed.");
            yield return new WaitForSecondsRealtime(SalonGameModel.MovingToStationSeconds + .25f);
            _owner.RuntimeGame.SelectCustomer(customer);
            _owner.RuntimeGame.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, _owner.HaircutSettings);
            if (!CompleteDryStep(_owner.RuntimeGame, customer))
                throw new InvalidOperationException("Candidate dry step failed.");
            yield return new WaitForSecondsRealtime(SalonGameModel.FinishedFeedbackSeconds + 1.2f);
            bool passed = customer.IsComplete &&
                          (customer.State == CustomerState.Leaving || customer.State == CustomerState.Exited) &&
                          _owner.RuntimeGame.Payments.Drops.Count > 0;
            _status = passed ? "候选工位完整服务回归通过" : "候选工位完整服务回归失败";
            Debug.Log((passed ? "[CANDIDATE_FLOW_PASS] " : "[CANDIDATE_FLOW_FAIL] ") +
                      $"customerState={customer.State} complete={customer.IsComplete} payment={_owner.RuntimeGame.Payments.Drops.Count}");
        }

        private static int FindAvailableHaircutStation(SalonGameModel game)
        {
            for (int index = 0; index < game.Workstations.Count; index++)
                if (game.Workstations[index].Type == WorkstationType.Haircut && !game.Workstations[index].Occupied)
                    return index;
            return -1;
        }

        public static bool ShouldConfigureHaircutOrder(CustomerModel customer)
            => customer != null && customer.CurrentNeed == ServiceType.Cut;

        public static bool PrepareHaircutTransfer(SalonGameModel game, CustomerModel customer)
        {
            if (game == null || customer == null || customer.CurrentNeed != ServiceType.Cut) return false;
            CustomerPhysicalStateSnapshot physical = game.GetServicePhysicalSnapshot(customer);
            if (physical.IsTowelWrapped &&
                game.PerformQuickAction(customer, ActiveServiceAction.RemoveTowel) != ServiceActionResult.QuickActionCompleted)
                return false;
            return !game.GetServicePhysicalSnapshot(customer).IsTowelWrapped;
        }

        public static bool CompleteDryStep(SalonGameModel game, CustomerModel customer)
        {
            if (game == null || customer == null || customer.CurrentNeed != ServiceType.Dry) return false;
            if (!game.StartManualBlow(customer)) return false;
            float duration = (game.ServiceConfig.ManualBlowGoodStart + game.ServiceConfig.ManualBlowGoodEnd) * .5f;
            if (!game.TickManualBlow(customer, duration)) return false;
            return game.EndManualBlowHold(customer) == BlowResult.Good;
        }

        private IEnumerator RunWash(CustomerModel customer, WashAction action, float duration, bool captureActive)
        {
            if (!_owner.RuntimeGame.BeginWashAction(customer, action))
                throw new InvalidOperationException("Candidate wash action failed to start: " + action);
            float elapsed = 0f;
            bool captured = false;
            while (customer.ActiveServiceAction != ActiveServiceAction.None && elapsed < duration + .5f)
            {
                float step = .1f;
                _owner.RuntimeGame.TickActiveServiceAction(customer, step);
                elapsed += step;
                if (captureActive && !captured && elapsed >= duration * .45f)
                {
                    captured = true;
                    _status = "候选洗发工位服务进行中";
                    Debug.Log("[CANDIDATE_SERVICE_ACTIVE] action=Shampoo customer=" + customer.Id);
                    yield return new WaitForSecondsRealtime(3f);
                }
                yield return new WaitForSecondsRealtime(.05f);
            }
        }

        private static bool QueryEnabled(string url, string key)
            => !string.IsNullOrWhiteSpace(url) &&
               url.IndexOf(key + "=1", StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 420f, 68f), "CANDIDATE ASSET VALIDATION\n" + _status);
        }
    }
}
