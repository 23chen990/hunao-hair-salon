using System.Linq;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>只解释已解析动作与订单事实；不提交业务状态，也不推导步骤顺序。</summary>
    public static class ServiceActionClassifier
    {
        public static ServiceActionClassification Classify(
            ActionResult result,
            OrderDefinition order,
            ServiceProgressSnapshot progress,
            CustomerPhysicalStateSnapshot physical)
        {
            if (result == null || order == null || progress == null ||
                result.ExecutionStatus != ExecutionStatus.Executed)
                return Classification(ServiceRelation.NormalService);

            ServiceActionType action = result.ActionToken?.ActionType ?? ServiceActionType.Unknown;
            ServiceRelation relation = ClassifyRelation(action, result, order, progress);
            ServiceActionClassificationReason reason = RelationReason(action, relation, result, order, progress);

            bool blowDryWithFoam = action == ServiceActionType.BlowDry &&
                                   physical != null && physical.FoamAmount > 0.001f;
            bool executionOver = result.ExecutionQuality == ExecutionQuality.Over;
            if (blowDryWithFoam) reason |= ServiceActionClassificationReason.BlowDryWithFoam;
            if (executionOver) reason |= ServiceActionClassificationReason.ExecutionOver;

            return Classification(relation, blowDryWithFoam || executionOver, reason);
        }

        private static ServiceRelation ClassifyRelation(
            ServiceActionType action,
            ActionResult result,
            OrderDefinition order,
            ServiceProgressSnapshot progress)
        {
            RequiredService? service = RequiredServiceFor(action);
            if (service.HasValue && !order.RequiredServices.Contains(service.Value))
                return ServiceRelation.WrongService;

            if (action == ServiceActionType.Haircut)
            {
                HaircutTool? requiredTool = CurrentRequiredHaircutTool(order, progress);
                if (!requiredTool.HasValue) return ServiceRelation.ExtraService;
                if (!TryHaircutTool(result.ActionToken.Tool, out HaircutTool selectedTool) ||
                    selectedTool != requiredTool.Value)
                    return ServiceRelation.WrongService;
            }

            if ((action == ServiceActionType.Shower || action == ServiceActionType.Shampoo) &&
                IsWashCompleted(progress))
                return ServiceRelation.ExtraService;
            if (action == ServiceActionType.BlowDry && progress[MilestoneId.HairDry].SatisfiedNow)
                return ServiceRelation.ExtraService;
            if (service.HasValue && result.OrderEffect == OrderEffect.ExtraService)
                return ServiceRelation.ExtraService;

            return ServiceRelation.NormalService;
        }

        private static ServiceActionClassificationReason RelationReason(
            ServiceActionType action,
            ServiceRelation relation,
            ActionResult result,
            OrderDefinition order,
            ServiceProgressSnapshot progress)
        {
            if (relation == ServiceRelation.ExtraService)
                return ServiceActionClassificationReason.CompletedServiceRepeated;
            if (relation != ServiceRelation.WrongService)
                return ServiceActionClassificationReason.None;

            RequiredService? service = RequiredServiceFor(action);
            if (service.HasValue && !order.RequiredServices.Contains(service.Value))
                return ServiceActionClassificationReason.UnrequestedService;
            if (action == ServiceActionType.Haircut &&
                CurrentRequiredHaircutTool(order, progress).HasValue &&
                (!TryHaircutTool(result.ActionToken.Tool, out HaircutTool selectedTool) ||
                 selectedTool != CurrentRequiredHaircutTool(order, progress).Value))
                return ServiceActionClassificationReason.WrongHaircutTool;
            return ServiceActionClassificationReason.UnrequestedService;
        }

        private static RequiredService? RequiredServiceFor(ServiceActionType action)
        {
            if (action == ServiceActionType.Shower || action == ServiceActionType.Shampoo)
                return RequiredService.Wash;
            if (action == ServiceActionType.BlowDry) return RequiredService.Dry;
            if (action == ServiceActionType.Haircut) return RequiredService.Cut;
            return null;
        }

        private static bool IsWashCompleted(ServiceProgressSnapshot progress)
        {
            return progress[MilestoneId.WetHairApplied].EverCompleted &&
                   progress[MilestoneId.Shampooed].EverCompleted &&
                   progress[MilestoneId.RinseClean].SatisfiedNow;
        }

        private static HaircutTool? CurrentRequiredHaircutTool(
            OrderDefinition order,
            ServiceProgressSnapshot progress)
        {
            foreach (HaircutTool tool in order.RequiredCutTools)
                if (!progress[HaircutMilestone(tool)].EverCompleted) return tool;
            return null;
        }

        private static MilestoneId HaircutMilestone(HaircutTool tool)
        {
            if (tool == HaircutTool.ThinningShears) return MilestoneId.ThinningShearsCompleted;
            return tool == HaircutTool.Clippers ? MilestoneId.ClippersCompleted : MilestoneId.ScissorsCompleted;
        }

        private static bool TryHaircutTool(ServiceTool tool, out HaircutTool haircutTool)
        {
            if (tool == ServiceTool.Scissors)
            {
                haircutTool = HaircutTool.Scissors;
                return true;
            }
            if (tool == ServiceTool.ThinningShears)
            {
                haircutTool = HaircutTool.ThinningShears;
                return true;
            }
            if (tool == ServiceTool.Clippers)
            {
                haircutTool = HaircutTool.Clippers;
                return true;
            }
            haircutTool = HaircutTool.Scissors;
            return false;
        }

        private static ServiceActionClassification Classification(
            ServiceRelation relation,
            bool disasterCandidate = false,
            ServiceActionClassificationReason reason = ServiceActionClassificationReason.None)
        {
            return new ServiceActionClassification(relation, disasterCandidate, reason);
        }
    }
}
