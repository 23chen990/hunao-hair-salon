using System;
using System.Collections;
using System.Collections.Generic;
using HairSalon;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    [Serializable]
    public sealed class BrowserCoreFlowReport
    {
        public bool StartedBusiness;
        public bool SpawnedCustomer;
        public bool AssignedStation;
        public bool CompletedService;
        public bool CustomerFinished;
        public bool PaymentCreated;
        public string Failure = string.Empty;

        public bool Passed => StartedBusiness && SpawnedCustomer && AssignedStation && CompletedService &&
                              CustomerFinished && PaymentCreated && string.IsNullOrEmpty(Failure);
    }

    public sealed class BrowserCoreFlowSmoke : MonoBehaviour
    {
        private SalonDemo _owner;
        private bool _captureServiceUi;

        public static bool IsRequested(string url)
            => !string.IsNullOrEmpty(url) &&
               url.IndexOf("browserSmoke=1", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsServiceUiEvidenceRequested(string url)
            => !string.IsNullOrEmpty(url) &&
               url.IndexOf("uiEvidence=service", StringComparison.OrdinalIgnoreCase) >= 0;

        public void Initialize(SalonDemo owner)
        {
            _owner = owner;
            _captureServiceUi = IsServiceUiEvidenceRequested(Application.absoluteURL);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var report = new BrowserCoreFlowReport();
            yield return null;
            _owner.RuntimeStartBusinessDay();
            yield return new WaitForSecondsRealtime(.2f);
            report.StartedBusiness = _owner.RuntimeDay.State == DayState.Business;
            if (!report.StartedBusiness)
            {
                Fail(report, "business did not start");
                yield break;
            }

            CustomerModel customer = _owner.RuntimeGame.Spawn(99001, new List<ServiceType> { ServiceType.Cut });
            report.SpawnedCustomer = customer != null;
            if (customer == null || !_owner.RuntimeGame.ConfigureHaircutOrder(customer, SalonTool.Scissors))
            {
                Fail(report, "customer spawn or haircut order setup failed");
                yield break;
            }
            _owner.RuntimeAttachCustomerView(customer);
            _owner.RuntimeDay.Stats.RecordSpawn(customer);
            yield return new WaitForSecondsRealtime(SalonGameModel.EnteringSeconds + .15f);

            int station = FindFreeHaircutStation();
            report.AssignedStation = station >= 0 && _owner.RuntimeGame.Assign(customer, station);
            if (!report.AssignedStation)
            {
                Fail(report, "haircut station assignment failed");
                yield break;
            }
            yield return new WaitForSecondsRealtime(SalonGameModel.MovingToStationSeconds + .15f);

            if (_captureServiceUi)
            {
                _owner.RuntimeFocusCustomer(customer);
                Debug.Log("[UI_SERVICE_READY] customer=" + customer.Id + " station=" + customer.Station +
                          " uiEvidence=service");
                yield return new WaitForSecondsRealtime(2f);
            }

            _owner.RuntimeGame.SelectCustomer(customer);
            _owner.RuntimeGame.ApplyHaircutResult(customer, SalonTool.Scissors,
                HaircutResult.Perfect, _owner.HaircutSettings);
            yield return new WaitForSecondsRealtime(.2f);
            report.CompletedService = customer.IsComplete;
            report.CustomerFinished = customer.State == CustomerState.Finished;
            report.PaymentCreated = _owner.RuntimeGame.Payments.Drops.Count > 0;
            Log(report);
        }

        private static void Fail(BrowserCoreFlowReport report, string failure)
        {
            report.Failure = failure;
            Log(report);
        }

        private static void Log(BrowserCoreFlowReport report)
            => Debug.Log((report.Passed ? "[BROWSER_CORE_FLOW_PASS] " : "[BROWSER_CORE_FLOW_FAIL] ") +
                         JsonUtility.ToJson(report));

        private int FindFreeHaircutStation()
        {
            for (int i = 0; i < _owner.RuntimeGame.Workstations.Count; i++)
                if (_owner.RuntimeGame.Workstations[i].Type == WorkstationType.Haircut &&
                    !_owner.RuntimeGame.IsStationOccupied(i)) return i;
            return -1;
        }
    }
}
