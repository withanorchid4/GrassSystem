using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BezierTest : MonoBehaviour
{
    // Start is called before the first frame update
    public Transform p0;
    public Transform p1;
    public Transform p2;
    public Transform p3;

    //para
    public float segments = 20f;
    public float gizmosSize = 0.1f;
    public Color curveColor = Color.green;
    public Color controlPointColor = Color.blue;

    public static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) //t是插值系数
    {
        //根据插值系数，从曲线中插值出一个点的计算函数
        float omt = 1f - t;
        float omt2 = omt * omt;
        float t2 = t * t;

        return p0 * (omt * omt2) +
                p1 * (3f * omt2 * t) +
                p2 * (3f * omt * t2) +
                p3 * (t * t2);
    }

    private void OnDrawGizmos()
    {
        if (p0 == null || p1 == null || p2 == null || p3 == null)
        {
            return;
        }
        Gizmos.color = controlPointColor;
        Gizmos.DrawSphere(p0.position, gizmosSize);
        Gizmos.DrawSphere(p1.position, gizmosSize);
        Gizmos.DrawSphere(p2.position, gizmosSize);
        Gizmos.DrawSphere(p3.position, gizmosSize);

        Gizmos.color = curveColor;
        Vector3 prePosition = p0.position;
        for(float i = 0; i < segments; i ++)
        {
            float t = i / segments;
            Vector3 curPoint = CubicBezier(p0.position, p1.position, p2.position, p3.position, t);
            Gizmos.DrawLine(prePosition, curPoint);
            prePosition = curPoint;
        }

    }
}
