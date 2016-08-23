// Position buffer format:
// .xyz = particle position
// .w   = life (+0.5 -> -0.5)
Shader "Makibishi/Standard"
{
    Properties
    {
        _PositionBuffer ("-", 2D) = "black"{}
        _RotationBuffer ("-", 2D) = "red"{}

        [KeywordEnum(Single, Animate, Random)]
        _ColorMode ("-", Float) = 0
        _Color     ("-", Color) = (1, 1, 1, 1)
        _Color2    ("-", Color) = (0.5, 0.5, 0.5, 1)

        _Metallic   ("-", Range(0,1)) = 0.5
        _Smoothness ("-", Range(0,1)) = 0.5

        _MainTex      ("-", 2D) = "white"{}
        _NormalMap    ("-", 2D) = "bump"{}
        _NormalScale  ("-", Range(0,2)) = 1
        _OcclusionMap ("-", 2D) = "white"{}
        _OcclusionStr ("-", Range(0,1)) = 1

        [HDR] _Emission ("-", Color) = (0, 0, 0)

        _ScaleMin ("-", Float) = 1
        _ScaleMax ("-", Float) = 1

        _RandomSeed ("-", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching" = "True" }

        CGPROGRAM

        #pragma surface surf Standard vertex:vert nolightmap addshadow
        #pragma shader_feature _COLORMODE_RANDOM
        #pragma shader_feature _ALBEDOMAP
        #pragma shader_feature _NORMALMAP
        #pragma shader_feature _OCCLUSIONMAP
        #pragma shader_feature _EMISSION
        #pragma multi_compile _ _USE_COMPUTESHADER
        #pragma multi_compile _ _WORLDSPACE
        #pragma target 5.0

        #include "../Cgincs/Math.cginc"
        #include "../Cgincs/Noise.cginc"
        #include "UnityCG.cginc"

        half _Metallic;
        half _Smoothness;

        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _OcclusionMap;
        half      _NormalScale;
        half      _OcclusionStr;
        half3     _Emission;

        half  _ColorMode;
        half4 _Color;
        half4 _Color2;
        float _ScaleMin;
        float _ScaleMax;
        float _RandomSeed;

        #if _USE_COMPUTESHADER
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
        float2 _BufferOffset;
        #endif

        struct Input
        {
            float2 uv_MainTex;
            half4 color : COLOR;
        };

        float calc_scale(float2 uv, float time01)
        {
            float s = lerp(_ScaleMin, _ScaleMax, rnd(uv, float2(14, _RandomSeed)));
            // Linear scaling animation with life.
            // (0, 0) -> (0.1, 1) -> (0.9, 1) -> (1, 0)
            return s * min(1.0, 5.0 - abs(5.0 - time01 * 10));
        }

        float4 calc_color(float2 uv, float time01)
        {
            #if _COLORMODE_RANDOM
            return lerp(_Color, _Color2, rnd(uv, float2(15, _RandomSeed)));
            #else
            return lerp(_Color, _Color2, (1.0 - time01) * _ColorMode);
            #endif
        }

        void vert(inout appdata_full v)
        {
            #if _USE_COMPUTESHADER
            int id = (int)v.texcoord1.x + (int)_EmitIdOffset;
            float4 position = _rwBuf[id].position;
            float4 rotation = _rwBuf[id].rotation;
            #else
            float2 id = float2(v.texcoord1.xy + _BufferOffset);
            float4 position = tex2Dlod(_PositionBuffer, float4(id,0,0));
            float4 rotation = tex2Dlod(_RotationBuffer, float4(id,0,0));
            #endif

            float life  = position.w + 0.5;
            float scale = calc_scale((float2)id, life);

            #if _WORLDSPACE
            v.vertex.xyz = qrotate(v.vertex.xyz, rotation) * scale + mul(_World2Object, float4(position.xyz, 1));
            #else
            v.vertex.xyz = qrotate(v.vertex.xyz, rotation) * scale + position.xyz;
            #endif

            v.normal = qrotate(v.normal, rotation);
            #if _NORMALMAP
            v.tangent.xyz = qrotate(v.tangent.xyz, r);
            #endif
            v.color = calc_color((float2)id, life);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
        #if _ALBEDOMAP
            half4 c = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = IN.color.rgb * c.rgb;
        #else
            o.Albedo = IN.color.rgb;
        #endif

        #if _NORMALMAP
            half4 n = tex2D(_NormalMap, IN.uv_MainTex);
            o.Normal = UnpackScaleNormal(n, _NormalScale);
        #endif

        #if _OCCLUSIONMAP
            half4 occ = tex2D(_OcclusionMap, IN.uv_MainTex);
            o.Occlusion = lerp((half4)1, occ, _OcclusionStr);
        #endif

        #if _EMISSION
            o.Emission = _Emission;
        #endif

            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
        }

        ENDCG
    }
    CustomEditor "MakibishiParticleSystem.SurfaceMaterialEditor"
}
