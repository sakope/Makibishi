Shader "Makibishi/SurfaceShader" {
    Properties{
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }
    SubShader{
        Tags { "RenderType" = "Opaque" "DisableBatching" = "True" }

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard vertex:vert nolightmap addshadow

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 5.0

        #include "UnityCG.cginc"
        #include "../Cgincs/Math.cginc"
        #include "../Cgincs/Noise.cginc"

        sampler2D _MainTex;

        struct Input {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float emitIdOffset;

        // Region Makibishi

        #pragma multi_compile _ _USE_COMPUTESHADER
        #pragma multi_compile _ _WORLDSPACE

        #ifdef _USE_COMPUTESHADER
        struct RewritableBuffer
        {
            float4 position;
            float4 velocity;
            float4 rotation;
        };
        #ifdef SHADER_API_D3D11
        StructuredBuffer<RewritableBuffer> _rwBuf;
        #endif
        float _EmitIdOffset;
        #else
        sampler2D _PositionBuffer;
        sampler2D _RotationBuffer;
        float _BufferOffset;
        #endif

        float _ScaleMin;
        float _ScaleMax;
        float _RandomSeed;

        float changescale(float2 seed, float time01)
        {
            float s = lerp(_ScaleMin, _ScaleMax, rnd(seed, float2(14, _RandomSeed)));
            // Linear scaling animation with life.
            // (0, 0) -> (0.1, 1) -> (0.9, 1) -> (1, 0)
            return s * min(1.0, 5.0 - abs(5.0 - time01 * 10));
        }

        void vert(inout appdata_full v)
        {
            #ifdef _USE_COMPUTESHADER
            int id = (int)v.texcoord1.x + (int)_EmitIdOffset;
            float4 position = _rwBuf[id].position;
            float4 rotation = _rwBuf[id].rotation;
            #else
            float2 id = float2(v.texcoord1.xy + _BufferOffset);
            float4 position = tex2Dlod(_PositionBuffer, float4(id,0,0));
            float4 rotation = tex2Dlod(_RotationBuffer, float4(id,0,0));
            #endif

            float life  = position.w + 0.5;
            float scale = changescale((float2)id, life);

            #if _WORLDSPACE
            v.vertex.xyz = qrotate(v.vertex.xyz, rotation) * scale + mul(_World2Object, float4(position.xyz,1));
            #else
            v.vertex.xyz = qrotate(v.vertex.xyz, rotation) * scale + position.xyz;
            #endif

            v.normal = qrotate(v.normal, rotation);
            #if _NORMALMAP
            v.tangent.xyz = qrotate(v.tangent.xyz, r);
            #endif
        }

        // Endregion Makibishi

        void surf(Input IN, inout SurfaceOutputStandard o) {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack off
}
