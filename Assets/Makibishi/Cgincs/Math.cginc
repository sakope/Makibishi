#ifndef MATH_INCLUDED
#define MATH_INCLUDED

//Matrix

float4x4 move(float3 move) {
    return float4x4(
        1, 0, 0, move.x,
        0, 1, 0, move.y,
        0, 0, 1, move.z,
        0, 0, 0, 1
        );
}

float4x4 scale(float3 scale) {
    return float4x4(
        scale.x, 0, 0, 0,
        0, scale.y, 0, 0,
        0, 0, scale.z, 0,
        0, 0, 0, 1
        );
}

float4x4 rotatex(float angle) {
    float s, c;
    sincos(angle, s, c);
    return float4x4(
        1, 0, 0, 0,
        0, c, -s, 0,
        0, s, c, 0,
        0, 0, 0, 1
        );
}

float4x4 rotatey(float angle) {
    float s, c;
    sincos(angle, s, c);
    return float4x4(
        c, 0, s, 0,
        0, 1, 0, 0,
        -s, 0, c, 0,
        0, 0, 0, 1
        );
}

float4x4 rotatez(float angle) {
    float s, c;
    sincos(angle, s, c);
    return float4x4(
        c, -s, 0, 0,
        s, c, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
        );
}

float4x4 connect3mat(float4x4 m1, float4x4 m2, float4x4 m3) {
    return mul(mul(m1, m2), m3);
}

float4x4 connect4mat(float4x4 m1, float4x4 m2, float4x4 m3, float4x4 m4) {
    return mul(mul(mul(m1, m2), m3), m4);
}

float4x4 rotate(float3 rotation) {
    float4x4 mx = rotatex(rotation.x);
    float4x4 my = rotatey(rotation.y);
    float4x4 mz = rotatez(rotation.z);
    return connect3mat(mx, my, mz);
}

//Quaternion

//axis has to be normalized.
float4 quaternion(float3 axis, float rad) {
    return float4(axis * sin(rad * 0.5), cos(rad * 0.5));
}

float4 qmul(float4 q1, float4 q2)
{
    return float4(
        q2.xyz * q1.w + q1.xyz * q2.w + cross(q1.xyz, q2.xyz),
        q1.w * q2.w - dot(q1.xyz, q2.xyz)
        );
}

float4 qconj(float4 q)
{
    return float4(-q.xyz, q.w);
}

float3 qrotate(float3 v, float4 q)
{
    return qmul(q, qmul(float4(v, 0), qconj(q))).xyz;
}

#endif //MATH_INCLUDED