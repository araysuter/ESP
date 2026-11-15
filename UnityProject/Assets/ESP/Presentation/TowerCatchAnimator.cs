using UnityEngine;

namespace ESP.Presentation
{
    // Approximate visual rig for the imported Starbase modules. No collision or capture logic.
    public sealed class TowerCatchAnimator : MonoBehaviour
    {
        Transform carriage, leftPivot, rightPivot;
        const float HighOffsetMeters = 50f;
        const float CarriageSpeedMetersPerSecond = 5f;
        float closure, targetClosure, targetHeight;
        float leftOpenAngle, rightOpenAngle;
        double previousSimulationTime;
        bool hasSimulationTime;
        public bool IsBound => leftPivot && rightPivot;
        public bool AtCatchHeight => carriage && Mathf.Abs(carriage.localPosition.y - HighOffsetMeters) < .05f;

        public static TowerCatchAnimator Attach(Transform towerRoot)
        {
            if (!towerRoot) return null;
            var existing = towerRoot.GetComponent<TowerCatchAnimator>();
            if (existing) return existing;
            var left = Find(towerRoot, "Left_catch_arm");
            var right = Find(towerRoot, "Right_catch_arm");
            if (!left || !right) return null;
            if (!TryArmGeometry(towerRoot, left, out Vector3 leftHinge, out float leftAngle) ||
                !TryArmGeometry(towerRoot, right, out Vector3 rightHinge, out float rightAngle) ||
                leftHinge.x * rightHinge.x >= 0)
            {
                Debug.LogError("ESP tower arms could not be bound to opposite outward-opening pivots.");
                return null;
            }
            var animator = towerRoot.gameObject.AddComponent<TowerCatchAnimator>();
            animator.carriage = new GameObject("Catch carriage visual rig").transform;
            animator.carriage.SetParent(towerRoot, false);
            animator.leftOpenAngle = leftAngle;
            animator.rightOpenAngle = rightAngle;
            animator.leftPivot = Pivot(animator.carriage, left, leftHinge, "Left arm pivot");
            animator.rightPivot = Pivot(animator.carriage, right, rightHinge, "Right arm pivot");
            animator.ResetWaitingPose();
            return animator;
        }
        public bool IsOpen => closure <= .001f;
        public void ResetWaitingPose()
        {
            simulationDriven = true;
            hasSimulationTime = false;
            closure = targetClosure = 0;
            targetHeight = HighOffsetMeters;
            carriage.localPosition = Vector3.up * HighOffsetMeters;
            Apply();
        }
        static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
        // Derive side from the imported mesh, not OBJ object names: Unity may
        // mirror the source's handedness. The tower occupies z≈-36 m and the
        // arms extend toward the mount at z≈0. Pivots are still approximations.
        static bool TryArmGeometry(Transform tower, Transform arm, out Vector3 hinge, out float angle)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (var renderer in arm.GetComponentsInChildren<Renderer>(true))
            {
                var b = renderer.localBounds;
                var matrix = tower.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            hinge = Vector3.zero; angle = 0;
            if (!found || Mathf.Abs(bounds.center.x) < .25f) return false;
            float side = Mathf.Sign(bounds.center.x);
            hinge = new Vector3(side * 6, 66, -29.2f);
            var tip = new Vector3(bounds.center.x, 66, bounds.max.z);
            var relative = tip - hinge;
            float positiveX = (hinge + Quaternion.Euler(0, 35, 0) * relative).x * side;
            float negativeX = (hinge + Quaternion.Euler(0, -35, 0) * relative).x * side;
            angle = positiveX > negativeX ? 35 : -35;
            // Reject ambiguous or crossing geometry rather than silently fold inwards.
            return Mathf.Max(positiveX, negativeX) > Mathf.Max(.25f, tip.x * side + .25f);
        }
        static Transform Pivot(Transform parent, Transform arm, Vector3 location, string name)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = location;
            arm.SetParent(pivot, true);
            return pivot;
        }
        // Closure is 0=open, 1=closed. Height is an offset from the imported 66 m pose.
        // Caller must gate closure on a verified capture; these pivots are approximate.
        public void SetTarget(float armClosure01, float carriageHeightMeters)
        {
            simulationDriven = false;
            hasSimulationTime = false;
            targetClosure = Mathf.Clamp01(armClosure01);
            targetHeight = Mathf.Clamp(carriageHeightMeters, -35, HighOffsetMeters);
        }
        bool simulationDriven;
        // Presentation follows elapsed simulation time, including time warp.
        // The flight controller must independently gate capture on carriage arrival.
        public void SetApproachPose(float armClosure01, double simulationTime)
        {
            simulationDriven = true;
            double dt = hasSimulationTime ? System.Math.Max(0, simulationTime - previousSimulationTime) : 0;
            previousSimulationTime = simulationTime;
            hasSimulationTime = true;
            var pos = carriage.localPosition;
            pos.y = Mathf.MoveTowards(pos.y, HighOffsetMeters,
                (float)System.Math.Min(dt * CarriageSpeedMetersPerSecond, HighOffsetMeters));
            carriage.localPosition = pos;
            // Open while rising; once ready, follow the controller's closure.
            float goal = AtCatchHeight ? Mathf.Clamp01(armClosure01) : 0;
            closure = Mathf.MoveTowards(closure, goal, (float)System.Math.Min(dt * .18, 1));
            targetClosure = goal;
            Apply();
        }
        public void SetSimulationPose(float armClosure01, float carriageHeightMeters)
        {
            simulationDriven = true;
            closure = targetClosure = Mathf.Clamp01(armClosure01);
            targetHeight = Mathf.Clamp(carriageHeightMeters, -35, HighOffsetMeters);
            carriage.localPosition = Vector3.up * targetHeight;
            Apply();
        }
        void Update()
        {
            if (simulationDriven) return;
            closure = Mathf.MoveTowards(closure, targetClosure, Time.deltaTime * .18f);
            var pos = carriage.localPosition;
            pos.y = Mathf.MoveTowards(pos.y, targetHeight, Time.deltaTime * 2f);
            carriage.localPosition = pos;
            Apply();
        }
        void Apply()
        {
            leftPivot.localRotation = Quaternion.Euler(0, leftOpenAngle * (1 - closure), 0);
            rightPivot.localRotation = Quaternion.Euler(0, rightOpenAngle * (1 - closure), 0);
        }
    }
}
