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
            var animator = towerRoot.gameObject.AddComponent<TowerCatchAnimator>();
            animator.carriage = new GameObject("Catch carriage visual rig").transform;
            animator.carriage.SetParent(towerRoot, false);
            animator.leftPivot = Pivot(animator.carriage, left, new Vector3(6, 66, -29.2f), "Left arm pivot");
            animator.rightPivot = Pivot(animator.carriage, right, new Vector3(-6, 66, -29.2f), "Right arm pivot");
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
            targetHeight = Mathf.Clamp(carriageHeightMeters, -35, 40);
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
            targetHeight = Mathf.Clamp(carriageHeightMeters, -35, 40);
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
            leftPivot.localRotation = Quaternion.Euler(0, 22 * (1 - closure), 0);
            rightPivot.localRotation = Quaternion.Euler(0, -22 * (1 - closure), 0);
        }
    }
}
