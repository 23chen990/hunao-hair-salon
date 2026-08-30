using System.Globalization;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    public sealed class DevelopmentDebugContext
    {
        public string Scene;
        public Vector3 CharacterPosition;
        public string CharacterState;
        public string ServiceState;
        public string TargetStation;
        public Vector2Int Viewport;
        public string BuildVersion;
    }

    public static class DevelopmentDebugSnapshot
    {
        public static string Serialize(DevelopmentDebugContext context)
        {
            context ??= new DevelopmentDebugContext();
            Vector3 p = context.CharacterPosition;
            return "{" +
                   "\"scene\":\"" + Escape(context.Scene) + "\"," +
                   "\"characterPosition\":{" +
                   "\"x\":" + Number(p.x) + ",\"y\":" + Number(p.y) + ",\"z\":" + Number(p.z) + "}," +
                   "\"characterState\":\"" + Escape(context.CharacterState) + "\"," +
                   "\"serviceState\":\"" + Escape(context.ServiceState) + "\"," +
                   "\"targetStation\":\"" + Escape(context.TargetStation) + "\"," +
                   "\"viewport\":\"" + context.Viewport.x + "x" + context.Viewport.y + "\"," +
                   "\"buildVersion\":\"" + Escape(context.BuildVersion) + "\"}";
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
