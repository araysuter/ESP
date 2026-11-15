using System.Collections.Generic;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    public sealed partial class RocketFlightDriver
    {
        // Display-only debris: doubles are relative to the same inertial origin as
        // the vessel. It never participates in authoritative collision or flight.
        private sealed class SeparatedStage
        {
            public Transform visual;
            public Vector3d position, velocity;
            public Vector3 visualOffset;
            public double created;
        }
        private readonly List<SeparatedStage> separatedStages = new List<SeparatedStage>();
        private int observedStage = -1;
        private bool observedBoosters;

        private void DetectSeparatedStages()
        {
            var state = simulation.State;
            if (observedStage >= 0 && state.IsLaunched && director != null && director.RocketVisual != null)
            {
                var rocket = director.RocketVisual;
                if (observedBoosters && !simulation.BoosterAttached)
                    foreach (string name in new[] { "LeftSRB", "RightSRB" }) SpawnSeparated(rocket.Find(name), state);
                if (state.StageIndex > observedStage)
                {
                    var view = rocket.GetComponent<RocketPresentation>();
                    if (view != null) SpawnSeparated(view.booster, state);
                }
            }
            observedStage = state.StageIndex;
            observedBoosters = simulation.BoosterAttached;
        }

        private void SpawnSeparated(Transform source, RocketSnapshot state)
        {
            if (source == null || !source.gameObject.activeInHierarchy) return;
            var visual = CopyStageMeshes(source, null);
            visual.name = "Separated stage (visual only)";
            visual.SetPositionAndRotation(source.position, source.rotation);
            visual.localScale = source.lossyScale;
            separatedStages.Add(new SeparatedStage { visual = visual, position = state.Position,
                velocity = state.Velocity, created = state.MissionTime,
                visualOffset = source.position - director.RocketVisual.position });
        }

        private static Transform CopyStageMeshes(Transform source, Transform parent)
        {
            var copy = new GameObject(source.name).transform;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition; copy.localRotation = source.localRotation; copy.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null && renderer.enabled && !RocketPlume.IsExhaustRenderer(renderer))
            {
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var mesh = copy.gameObject.AddComponent<MeshRenderer>();
                mesh.sharedMaterials = renderer.sharedMaterials;
                mesh.shadowCastingMode = renderer.shadowCastingMode; mesh.receiveShadows = renderer.receiveShadows;
            }
            foreach (Transform child in source)
                if (child.gameObject.activeSelf && child.name != "LeftSRB" && child.name != "RightSRB") CopyStageMeshes(child, copy);
            return copy;
        }

        private void AdvanceSeparatedStages(double dt)
        {
            double time = simulation.State.MissionTime;
            foreach (var piece in separatedStages)
            {
                var acceleration = simulation.Celestial != null
                    ? simulation.Celestial.Acceleration(piece.position, time, simulation.GravityMultiplier)
                    : Gravity.Acceleration(piece.position) * simulation.GravityMultiplier;
                piece.position += piece.velocity * dt + acceleration * (dt * dt * .5);
                piece.velocity += acceleration * dt;
            }
        }

        private void RenderSeparatedStages(Vector3d vesselEcef, double lat, double lon, double time)
        {
            if (director.RocketVisual == null) return;
            for (int i = separatedStages.Count - 1; i >= 0; i--)
            {
                var piece = separatedStages[i];
                if ((piece.position - simulation.State.Position).Magnitude() > 10000 || time - piece.created > 600)
                { Destroy(piece.visual.gameObject); separatedStages.RemoveAt(i); continue; }
                var ecef = EarthFrames.InertialToEcef(piece.position, Aerodynamics.EarthAngularVelocityRadiansPerSecond * time);
                piece.visual.position = director.RocketVisual.position + piece.visualOffset + CameraRelativeCoordinates.FromEcef(
                    ecef.X, ecef.Y, ecef.Z, vesselEcef.X, vesselEcef.Y, vesselEcef.Z, lat, lon);
            }
        }

        private void ClearSeparatedStages()
        {
            foreach (var piece in separatedStages) if (piece.visual != null) Destroy(piece.visual.gameObject);
            separatedStages.Clear(); observedStage = -1; observedBoosters = false;
        }
    }
}
