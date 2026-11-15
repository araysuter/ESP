/*
 * Copyright (c) 2019, Sébastien GAGGINI AKA Sarbian, France
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice, this
 *    list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 */

// Adapted from SmokeScreen PersistentKSPShurikenEmitter.cs emission accumulation
// and sub-frame declustering, revision 245afb49063f48a8beb4289986aaccc0446700ad.
// ESP changes: deterministic cadence, bounded newest-first retention, explicit time,
// and no KSP, Unity ParticleSystem, or global random-state dependencies.
using System;

namespace ESP.Presentation
{
    public readonly struct SmokeEmissionBatch
    {
        public readonly int Count;
        public readonly double OldestAgeSeconds, IntervalSeconds;
        public SmokeEmissionBatch(int count, double age, double interval)
        { Count = count; OldestAgeSeconds = age; IntervalSeconds = interval; }
        public double AgeSeconds(int index) => Math.Max(0, OldestAgeSeconds - index * IntervalSeconds);
    }

    public sealed class SmokeEmissionSchedule
    {
        private double fractional;
        public void Clear() => fractional = 0;
        public SmokeEmissionBatch Advance(double seconds, double ratePerSecond, int limit)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0 ||
                double.IsNaN(ratePerSecond) || double.IsInfinity(ratePerSecond) || ratePerSecond <= 0 || limit <= 0)
                return default;
            double total = fractional + seconds * ratePerSecond;
            if (double.IsInfinity(total)) { fractional = 0; return default; }
            double due = Math.Floor(total + 1e-10);
            fractional = Math.Max(0, Math.Min(1, total - due));
            int count = (int)Math.Min(limit, due);
            // Discard excess old emissions now, so a hitch cannot leave a spawn backlog.
            // Retained births are evenly spaced across the newest part of the interval.
            return new SmokeEmissionBatch(count,
                count > 0 ? (fractional + count - 1) / ratePerSecond : 0, 1 / ratePerSecond);
        }
    }
}

