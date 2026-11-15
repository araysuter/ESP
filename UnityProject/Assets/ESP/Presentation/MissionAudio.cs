using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    [RequireComponent(typeof(AudioListener))]
    public sealed class MissionAudio : MonoBehaviour
    {
        private const float MusicVolume = 0.23f;
        private const float DuckedMusicVolume = 0.08f;
        private const int StarshipVehicleIndex = 1;

        private RocketFlightDriver flight;
        private RocketSimulation mission;
        private AudioSource music;
        private AudioSource liftoff;
        private bool playedLiftoff;
        private bool liftoffPaused;

        private void Awake()
        {
            flight = GetComponent<RocketFlightDriver>();
            music = CreateSource("MagicSpace", MusicVolume, loop: true);
            liftoff = CreateSource("StarshipLiftoff", 0.85f, loop: false);
        }

        private void OnEnable()
        {
            if (music.clip != null) music.Play();
        }

        private void OnDisable()
        {
            music.Stop();
            StopLiftoff();
        }

        private void LateUpdate()
        {
            if (mission != flight.Simulation)
            {
                StopLiftoff();
                mission = flight.Simulation;
                playedLiftoff = false;
            }

            if (mission != null && flight.director != null && (flight.director.VehicleIndex == StarshipVehicleIndex || flight.director.VehicleIndex == 3))
                UpdateLiftoff();

            float target = liftoff.isPlaying ? DuckedMusicVolume : MusicVolume;
            music.volume = Mathf.MoveTowards(music.volume, target, 0.4f * Time.unscaledDeltaTime);
        }

        private void UpdateLiftoff()
        {
            if (liftoff.clip == null || (playedLiftoff && !liftoff.isPlaying && !liftoffPaused)) return;

            var state = mission.State;
            if (state.IsLanded)
            {
                StopLiftoff();
                return;
            }

            if (flight.IsPaused || MissionSession.BlocksInput)
            {
                if (liftoff.isPlaying)
                {
                    liftoff.Pause();
                    liftoffPaused = true;
                }
                return;
            }

            if (liftoffPaused)
            {
                liftoff.UnPause();
                liftoffPaused = false;
            }
            else if (!playedLiftoff && state.IsLaunched)
            {
                playedLiftoff = true;
                liftoff.Play();
            }
        }

        private void StopLiftoff()
        {
            liftoff.Stop();
            liftoffPaused = false;
        }

        private AudioSource CreateSource(string clipName, float volume, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = Resources.Load<AudioClip>("MissionAudio/" + clipName);
            if (source.clip == null) Debug.LogError("Missing mission audio clip " + clipName);
            source.spatialBlend = 0;
            source.loop = loop;
            source.volume = volume;
            return source;
        }
    }
}
