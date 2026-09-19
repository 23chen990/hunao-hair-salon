using System;
using System.Collections;
using System.Collections.Generic;
using HairSalon;
using HairSalon.ServiceArchitecture;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    // Explicit development parameters only; no production HUD or automated play in normal builds.
    public sealed class WashCraftEvidence : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private SalonDemo _owner;
        private bool _detail;
        private bool _serviceEvidence;
        private int _station;
        private bool _active;
        public void Initialize(SalonDemo owner)
        {
            string url = Application.absoluteURL;
            if (!url.Contains("washCraft=detail") && !url.Contains("washCraft=service")) return;
            _owner = owner;
            _detail = true;
            _active = true;
            _serviceEvidence = url.Contains("washCraft=service");
            _station = url.Contains("station=4") ? 4 : 0;
            StartCoroutine(Run(_serviceEvidence, _station));
        }
        private void LateUpdate()
        {
            if (!_active || !_detail || Camera.main == null) return;
            var camera = Camera.main;
            Vector3 target = new Vector3(-6.15f + (_serviceEvidence && _station == 4 ? 2.6f : 0f), _serviceEvidence ? 2.6f : 1.85f, 4.8f);
            camera.transform.position = target + new Vector3(-10f, 13f, -17f);
            camera.transform.LookAt(target);
            camera.orthographicSize = _serviceEvidence ? 5.2f : 4.0f;
        }
        private IEnumerator Run(bool service, int station)
        {
            yield return null;
            _owner.RuntimeStartBusinessDay();
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log("[WASH_CRAFT_DETAIL_READY]");
            if (!service) yield break;
            CustomerModel customer = _owner.RuntimeGame.Spawn(88300 + station,
                new List<ServiceType> { ServiceType.Wash, ServiceType.Cut, ServiceType.Dry });
            if (customer == null) throw new InvalidOperationException("Craft wash regression could not spawn customer.");
            _owner.RuntimeAttachCustomerView(customer);
            _owner.RuntimeDay.Stats.RecordSpawn(customer);
            yield return new WaitForSecondsRealtime(SalonGameModel.EnteringSeconds + .2f);
            if (!_owner.RuntimeGame.Assign(customer, station)) throw new InvalidOperationException("Craft wash assignment failed.");
            yield return new WaitForSecondsRealtime(SalonGameModel.MovingToStationSeconds + .25f);
            _owner.RuntimeGame.SelectCustomer(customer);
            _owner.RuntimeFocusCustomer(customer);
            Transform stylist = GameObject.Find("主控理发师").transform;
            Vector3 workPosition = (station == 4 ? SalonDemo.SecondaryWashPlayerAnchorPosition : SalonDemo.WashPlayerAnchorPosition);
            workPosition.y = stylist.position.y;
            // Observe actual movement; software WebGL/video capture need not advance at wall-clock speed.
            float arrivalDeadline = Time.realtimeSinceStartup + 20f;
            while (Vector3.Distance(stylist.position, workPosition) > .15f && Time.realtimeSinceStartup < arrivalDeadline)
                yield return null;
            float arrivalError = Vector3.Distance(stylist.position, workPosition);
            if (arrivalError > .15f) throw new InvalidOperationException("Stylist has not reached wash work anchor: " + arrivalError);
            Debug.Log("[WASH_CRAFT_SEATED] station=" + station + " stylistArrivalError=" + arrivalError.ToString("F3"));
            yield return new WaitForSecondsRealtime(2f);
            yield return Wash(customer, WashAction.Shower, false);
            yield return Wash(customer, WashAction.Shampoo, true);
            yield return Wash(customer, WashAction.Shower, false);
            if (_owner.RuntimeGame.PerformQuickAction(customer, ActiveServiceAction.WrapTowel) != ServiceActionResult.QuickActionCompleted)
                throw new InvalidOperationException("Craft towel step failed.");
            Debug.Log("[WASH_CRAFT_WASH_DONE] station=" + station);
            yield return new WaitForSecondsRealtime(2f);
            if (!CandidateAssetValidation.PrepareHaircutTransfer(_owner.RuntimeGame, customer))
                throw new InvalidOperationException("Craft transfer preparation failed.");
            int cut = -1;
            for (int i = 0; i < _owner.RuntimeGame.Workstations.Count; i++)
                if (_owner.RuntimeGame.Workstations[i].Type == WorkstationType.Haircut && !_owner.RuntimeGame.Workstations[i].Occupied) { cut = i; break; }
            if (cut < 0 || !_owner.RuntimeGame.Assign(customer, cut)) throw new InvalidOperationException("Craft haircut transfer failed.");
            _detail = false;
            yield return new WaitForSecondsRealtime(SalonGameModel.MovingToStationSeconds + .3f);
            _owner.RuntimeGame.SelectCustomer(customer);
            _owner.RuntimeGame.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, _owner.HaircutSettings);
            if (!CandidateAssetValidation.CompleteDryStep(_owner.RuntimeGame, customer)) throw new InvalidOperationException("Craft blow step failed.");
            float completionDeadline = Time.realtimeSinceStartup + 20f;
            while (!(customer.IsComplete && (customer.State == CustomerState.Leaving || customer.State == CustomerState.Exited) &&
                     _owner.RuntimeGame.Payments.Drops.Count > 0) && Time.realtimeSinceStartup < completionDeadline)
                yield return null;
            bool passed = customer.IsComplete && (customer.State == CustomerState.Leaving || customer.State == CustomerState.Exited) &&
                          _owner.RuntimeGame.Payments.Drops.Count > 0;
            Debug.Log((passed ? "[WASH_CRAFT_FLOW_PASS]" : "[WASH_CRAFT_FLOW_FAIL]") + " station=" + station + " payment=" + _owner.RuntimeGame.Payments.Drops.Count + " state=" + customer.State + " complete=" + customer.IsComplete);
        }
        private IEnumerator Wash(CustomerModel customer, WashAction action, bool capture)
        {
            if (!_owner.RuntimeGame.BeginWashAction(customer, action)) throw new InvalidOperationException("Craft wash could not start " + action);
            bool captured = false;
            float elapsed = 0f;
            while (customer.ActiveServiceAction != ActiveServiceAction.None && elapsed < 15f)
            {
                _owner.RuntimeGame.TickActiveServiceAction(customer, .10f);
                elapsed += .10f;
                if (capture && !captured && elapsed > .6f)
                {
                    captured = true;
                    Debug.Log("[WASH_CRAFT_SERVICE_ACTIVE]");
                    yield return new WaitForSecondsRealtime(3f);
                }
                yield return new WaitForSecondsRealtime(.08f);
            }
            if (customer.ActiveServiceAction != ActiveServiceAction.None) throw new InvalidOperationException("Craft wash action timed out.");
        }
#else
        public void Initialize(SalonDemo owner) { }
#endif
    }
}
