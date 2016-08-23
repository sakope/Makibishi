// Position buffer format:
// .xyz = particle position
// .w   = life (+0.5 -> -0.5)
//
// Velocity buffer format:
// .xyz = particle velocity
//
// Rotation buffer format:
// .xyzw = particle rotation
//
Shader "Makibishi/Compute"
{
    Properties
    {
        _PositionBuffer("-", 2D) = ""{}
        _VelocityBuffer("-", 2D) = ""{}
        _RotationBuffer("-", 2D) = ""{}
    }

    CGINCLUDE

    #include "UnityCG.cginc"
    #include "../Cgincs/Math.cginc"
    #include "../Cgincs/Noise.cginc"
    #include "../Cgincs/SimplexNoiseGrad3D.cginc"

    sampler2D _PositionBuffer;
    sampler2D _VelocityBuffer;
    sampler2D _RotationBuffer;

    float3 emitterPos;
    float3 emitterSize;
    float2 lifeParams;   // 1/min, 1/max
    float4 direction;    // x, y, z, spread
    float2 speedParams;  // speed, randomness
    float4 acceleration; // x, y, z, drag
    float3 spinParams;   // spin*2, speed-to-spin*2, randomness
    float2 noiseParams;  // freq, amp
    float3 noiseOffset;
    float4 config;       /// isLoop(loop is 0, one shot is 1), random seed, delta time, time

    float4 InitializePosition(float2 uv)
    {
        float t = config.w;

        float3 p = float3(rnd(uv, float2(t, config.y)), rnd(uv, float2(t + 1, config.y)), rnd(uv, float2(t + 2, config.y)));
        p = (p - (float3)0.5) * emitterSize + emitterPos;

        return float4(p, 0.5);
    }

    float4 InitializeVelocity(float2 uv)
    {
        float3 v = float3(rnd(uv, float2(6, config.y)), rnd(uv, float2(7, config.y)), rnd(uv, float2(8, config.y)));
        v = (v - (float3)0.5) * 2;

        v = lerp(direction.xyz, v, direction.w);

        v = normalize(v) * speedParams.x;
        v *= 1.0 - rnd(uv, float2(9, config.y)) * speedParams.y;

        return float4(v, 0);
    }

    float4 InitializeRotation(float2 uv)
    {
        // Uniform random unit quaternion
        // http://www.realtimerendering.com/resources/GraphicsGems/gemsiii/urot.c
        float r = rnd(uv, float2(3, config.y));
        float r1 = sqrt(1.0 - r);
        float r2 = sqrt(r);
        float t1 = UNITY_PI * 2 * rnd(uv, float2(4, config.y));
        float t2 = UNITY_PI * 2 * rnd(uv, float2(5, config.y));
        return float4(sin(t1) * r1, cos(t1) * r1, sin(t2) * r2, cos(t2) * r2);
    }

    float3 GetRotationAxis(float2 uv)
    {
        // Uniformaly distributed points
        // http://mathworld.wolfram.com/SpherePointPicking.html
        float u = rnd(uv, float2(10, config.y)) * 2 - 1;
        float theta = rnd(uv, float2(11, config.y)) * UNITY_PI * 2;
        float u2 = sqrt(1 - u * u);
        return float3(u2 * cos(theta), u2 * sin(theta), u);
    }

    // Pass 0
    float4 FragInitializePosition(v2f_img i) : SV_Target
    {
        return InitializePosition(i.uv) - float4(0, 0, 0, rnd(i.uv, float2(14, config.y)));
    }

    // Pass 1
    float4 FragInitializeVelocity(v2f_img i) : SV_Target
    {
        return InitializeVelocity(i.uv);
    }

    // Pass 2
    float4 FragInitializeRotation(v2f_img i) : SV_Target
    {
        return InitializeRotation(i.uv);
    }

    // Pass 3
    float4 FragUpdatePosition(v2f_img i) : SV_Target
    {
        float4 p = tex2D(_PositionBuffer, i.uv);
        float3 v = tex2D(_VelocityBuffer, i.uv).xyz;

        float dt = config.z;
        p.w -= lerp(lifeParams.x, lifeParams.y, rnd(i.uv, float2(12, config.y))) * dt;

        if (p.w > -0.5)
        {
            p.xyz += v * dt;
            return p;
        }
        else
        {
            float4 remove = float4(1e8, 1e8, 1e8, -1) * config.x;
            return InitializePosition(i.uv) + remove;
        }
    }

    // Pass 4
    float4 FragUpdateVelocity(v2f_img i) : SV_Target
    {
        float4 p = tex2D(_PositionBuffer, i.uv);
        float3 v = tex2D(_VelocityBuffer, i.uv).xyz;

        if (p.w < 0.5)
        {
            v *= acceleration.w; // dt is pre-applied in script

            float dt = config.z;
            v += acceleration.xyz * dt;

            float3 np = (p.xyz + noiseOffset) * noiseParams.x;
            float3 n1 = snoise_grad(np);
            float3 n2 = snoise_grad(np + float3(0, 13.28, 0));
            v += cross(n1, n2) * noiseParams.y * dt;

            return float4(v, 0);
        }
        else
        {
            return InitializeVelocity(i.uv);
        }
    }

    // Pass 5
    float4 FragUpdateRotation(v2f_img i) : SV_Target
    {
        float4 r = tex2D(_RotationBuffer, i.uv);
        float3 v = tex2D(_VelocityBuffer, i.uv).xyz;

        float dt = config.z;
        float theta = (spinParams.x + length(v) * spinParams.y) * dt;

        theta *= 1.0 - rnd(i.uv, float2(13, config.y)) * spinParams.z;

        float4 dq = float4(GetRotationAxis(i.uv) * sin(theta), cos(theta));

        return normalize(qmul(dq, r));
    }

    ENDCG

    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment FragInitializePosition
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment FragInitializeVelocity
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment FragInitializeRotation
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment FragUpdatePosition
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment FragUpdateVelocity
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment FragUpdateRotation
            ENDCG
        }
    }
}
