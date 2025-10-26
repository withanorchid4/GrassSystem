#ifndef CUBIC_BEZIER_INCLUDED
#define CUBIC_BEZIER_INCLUDED

//下面两个函数都是根据t插值bezier曲线上的点
float3 CubicBezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
{
    //公式版本
    float omt = 1 - t;
    float omt2 = omt * omt;
    float t2 = t * t;

    return p0 * (omt * omt2) +
            p1 * (3 * omt2 * t) +
            p2 * (3 * omt * t2) +
            p3 * (t * t2);
}

float3 CubicBezier2(float3 p0, float3 p1, float3 p2, float3 p3, float t)
{
    //更直观的插值版本
    float3 a = lerp(p0, p1, t);
    float3 b = lerp(p2, p3, t);
    float3 c = lerp(p1, p2, t);
    float3 d = lerp(a, c, t);
    float3 e = lerp(c, b, t);
    return lerp(d,e,t); 
}

//求t处的切线的函数（已知切线，用其和(0,0,1)叉乘便可得到法线）
float3 CubicBezierTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
{
    //通过计算bezier函数的导数计算切线，bezier函数是t的函数，p0p1p2p3都是参数，故导函数也是t的函数
    float omt = 1 - t;
    float omt2 = omt * omt;
    float t2 = t * t;

    float3 tangent =
            p0 * (-omt2) +
            p1 * (3 * omt2 - 2 *omt) +
            p2 * (-3 * t2 + 2 * t) +
            p3 * (t2);

    return normalize(tangent);
}

#endif // CUBIC_BEZIER_INCLUDED