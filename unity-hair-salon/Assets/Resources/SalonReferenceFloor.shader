Shader "HairSalon/ReferenceFloor"
{
    Properties
    {
        _FloorColor ("Floor Color", Color) = (0.25,0.52,0.52,1)
        _GroutColor ("Grout Color", Color) = (0.18,0.40,0.41,1)
        _TileCount ("Tile Count", Vector) = (56,36,0,0)
        _GroutWidth ("Grout Width", Range(0.005,0.15)) = 0.035
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _FloorColor;
            fixed4 _GroutColor;
            float4 _TileCount;
            float _GroutWidth;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 tiled = input.uv * _TileCount.xy;
                float2 local = frac(tiled);
                float gridLine = step(local.x, _GroutWidth) + step(local.y, _GroutWidth);
                float variation = (hash21(floor(tiled)) - 0.5) * 0.055;
                fixed3 floorColor = saturate(_FloorColor.rgb + variation);
                return fixed4(lerp(floorColor, _GroutColor.rgb, saturate(gridLine)), 1);
            }
            ENDCG
        }
    }
}
