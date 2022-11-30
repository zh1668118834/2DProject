using System.Collections.Generic;
using Game.Generic;
using UnityEngine;

public class QuadTreeUnityTest : MonoBehaviour
{

    class Unit : IBounds
    {

        public GameObject go;

        System.Numerics.Vector2 ltPos;

        System.Numerics.Vector2 rbPos;

        public System.Numerics.Vector2 LTPos => ltPos;
        public System.Numerics.Vector2 RBPos => rbPos;
        // public System.Numerics.Vector2 LTPos => new System.Numerics.Vector2(go.transform.position.x - go.transform.localScale.x / 2f, go.transform.position.y + go.transform.localScale.y / 2f);
        // public System.Numerics.Vector2 RBPos => new System.Numerics.Vector2(go.transform.position.x + go.transform.localScale.x / 2f, go.transform.position.y - go.transform.localScale.y / 2f);

        public void UpdateBounds()
        {
            var tf = go.transform;
            var unitPos = tf.position;
            var unitScale = tf.localScale;
            var unitLTPos = new System.Numerics.Vector2(unitPos.x - unitScale.x / 2f, unitPos.y + unitScale.y / 2f);
            var unitRBPos = new System.Numerics.Vector2(unitPos.x + unitScale.x / 2f, unitPos.y - unitScale.y / 2f);
            ltPos = unitLTPos;
            rbPos = unitRBPos;
        }

    }

    QuadTree<Unit> quadTree;
    public float quadLen;
    public int quadTreeLayer;
    public float searchWidth;
    public float searchHeight;
    public GameObject billboard;

    void Start()
    {
        quadTree = new QuadTree<Unit>(quadLen, quadTreeLayer, System.Numerics.Vector2.Zero);
        billboard.transform.localScale = new Vector3(quadLen, quadLen, 1);
        Camera.main.orthographicSize = quadLen / 2f;
    }

    int index = 0;
    bool unitCreated;
    Unit unit;
    Vector3 unitStartPos;
    Vector3 unitEndPos;

    void Update()
    {
        isRunning = true;

        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit downHit, 1000))
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var pos = downHit.point;
                pos.z = 0;
                unitStartPos = pos;
                go.transform.position = pos;
                go.transform.name = $"unit_{index++}";
                unit = new Unit();
                unit.go = go;
                unitCreated = true;
            }
        }

        if (Input.GetMouseButtonUp(0) && unitCreated)
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit upHit, 1000))
            {
                var pos = upHit.point;
                pos.z = 0;
                unitEndPos = pos;

                var scale_x = Mathf.Abs(unitEndPos.x - unitStartPos.x);
                scale_x = scale_x == 0 ? 1 : scale_x;
                var scale_y = Mathf.Abs(unitStartPos.y - unitEndPos.y);
                scale_y = scale_y == 0 ? 1 : scale_y;

                var tf = unit.go.transform;
                tf.localScale = new Vector3(scale_x, scale_y, 1);
                tf.position = (unitStartPos + unitEndPos) / 2f;

                unit.UpdateBounds();
                unitCreated = false;

                quadTree.AddUnit(unit);
            }
        }

        quadTree.Tick();
        TickUnitPosition();
    }

    public void TickUnitPosition()
    {
        quadTree.ForeachUnit((unit) =>
        {
            unit.Value.UpdateBounds();
        });
    }

    bool isRunning = false;
    void OnDrawGizmos()
    {
        if (!isRunning)
        {
            return;
        }

        DrawQuadTree();
        DrawSearchArea();
    }

    void DrawSearchArea()
    {
        Gizmos.color = Color.red;
        var widthOffset = searchWidth / 2f;
        var heightOffset = searchHeight / 2f;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000))
        {
            var mouseWorldPosX = hit.point.x;
            var mouseWorldPosY = hit.point.y;
            var ltPos = new Vector2(mouseWorldPosX - widthOffset, mouseWorldPosY + heightOffset);
            var rbPos = new Vector2(mouseWorldPosX + widthOffset, mouseWorldPosY - heightOffset);
            List<Quad<Unit>> quadList = new List<Quad<Unit>>();
            quadTree.GetAABBCollsionQuadList(ltPos.ToSystemVector2(), rbPos.ToSystemVector2(), quadList, 0);
            quadList.ForEach((quad) =>
            {
                var locationKey = QuadTree.GetLocationKey(quad.nodeIndex, quad.quadIndex);
                var units = quadTree.GetUnitList(locationKey);
                units.ForEach((unit) =>
                {
                    var tf = unit.Value.go.transform;
                    Gizmos.DrawCube(tf.position, tf.localScale);
                });
            });

            Gizmos.color = Color.white;
            var lbPos = ltPos + new Vector2(0, -searchHeight);
            var rtPos = rbPos + new Vector2(0, +searchHeight);
            Gizmos.DrawLine(lbPos, rbPos);
            Gizmos.DrawLine(ltPos, rtPos);
            Gizmos.DrawLine(ltPos, lbPos);
            Gizmos.DrawLine(rtPos, rbPos);

        }
    }


    void DrawQuadTree()
    {
        var e = quadTree.quadDic.GetEnumerator();
        while (e.MoveNext())
        {
            var kvp = e.Current;
            var key = kvp.Key;
            var quad = kvp.Value;
            DrawQuad(quad);
        }

    }

    void DrawQuad(Quad<Unit> quad)
    {
        var ltp = quad.ltPos;
        var rbp = quad.rbPos;
        Vector3 ltPos = new Vector3(ltp.X, ltp.Y, 0);
        Vector3 rbPos = new Vector3(rbp.X, rbp.Y, 0);
        var len = rbp.X - ltp.X;

        Vector3 rtPos = ltPos;
        rtPos.x += len;
        Vector3 lbPos = rbPos;
        lbPos.x -= len;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(ltPos, rtPos);
        Gizmos.DrawLine(lbPos, rbPos);
        Gizmos.DrawLine(ltPos, lbPos);
        Gizmos.DrawLine(rtPos, rbPos);
    }

}
