Shader "Serp/ProjectileTrails"
{
    Properties { _MainTex ("Sprite atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "DisableBatching" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            struct Echo
            {
                float2 positionXY;
                float positionZ;
                uint shape;
                uint tick;
                uint vertexCount;
            };

            StructuredBuffer<Echo> _SerpTrailEchoes;
            StructuredBuffer<float4> _SerpTrailGeometry;
            uint _SerpTrailBegin, _SerpTrailCount, _SerpTrailVertexStride;
            uint _SerpTrailTick, _SerpTrailLifetime;
            float4 _SerpTrailBasis;
            sampler2D _MainTex;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float opacity : TEXCOORD1;
            };

            Varyings Vertex(uint vertex : SV_VertexID)
            {
                Varyings result = (Varyings)0;
                result.position = float4(0, 0, 0, 1);
                uint instance = vertex / _SerpTrailVertexStride;
                if (instance >= _SerpTrailCount) return result;
                Echo echo = _SerpTrailEchoes[_SerpTrailBegin + instance];
                uint corner = vertex % _SerpTrailVertexStride;
                uint age = _SerpTrailTick - echo.tick;
                if (corner >= echo.vertexCount || age >= _SerpTrailLifetime) return result;

                float4 cornerData = _SerpTrailGeometry[echo.shape + corner];
                float3 world = float3(echo.positionXY, echo.positionZ) + float3(cornerData.x * _SerpTrailBasis.xy + cornerData.y * _SerpTrailBasis.zw, 0);
                result.position = mul(UNITY_MATRIX_VP, float4(world, 1));
                result.uv = cornerData.zw;
                uint transparency = min(age / 3u, 2u) * 32u / 3u;
                result.opacity = (32u - transparency) / 32.0;
                return result;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 color = tex2D(_MainTex, input.uv);
                color.a *= input.opacity;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
