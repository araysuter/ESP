# FlightCore Gravity Library

This library provides Earth-centered vector calculations for gravitational acceleration using Newton's law of universal gravitation.

## Overview

The `Gravity` class implements the calculation of gravitational acceleration at nonzero Earth-centered positions, using the standard gravitational parameter and inverse square law.

## Usage

```csharp
using FlightCore;

// Calculate gravitational acceleration at a specific position
var position = new Vector3d(6378137.0, 0.0, 0.0); // At Earth surface
var acceleration = Gravity.Acceleration(position);
```

## Technical Details

### Gravitational Model

- **Earth Gravitational Parameter (μ)**: 3.986004418 × 10¹⁴ m³/s²
- **Earth Equatorial Radius**: 6,378,137 meters
- **Formula**: **a** = -μ × **r** / |**r**|³

### Input Validation

The library validates all inputs:
- Throws `ArgumentException` for zero vectors (division by zero)
- Throws `ArgumentException` for NaN or infinity values
- The implementation is intended for Earth-scale positions (surface through orbital distances). Arbitrary finite double values are not guaranteed: intermediate calculations can overflow or underflow.

### Test Coverage

The implementation includes focused test cases:
1. **Earth Surface Position**: Tests acceleration at Earth's surface (6378137 m, 0, 0)
2. **Off-Axis Position**: Verifies acceleration vectors point toward Earth center
3. **Distance-Doubling Rule**: Confirms inverse square law behavior (acceleration decreases by factor of 4 when distance doubles)
4. **Invalid Inputs**: Ensures proper exception handling for edge cases

## API Reference

### `Gravity.Acceleration(Vector3d position)`

Calculates the gravitational acceleration vector at the given position.

**Parameters:**
- `position`: The 3D vector representing the position in meters relative to Earth's center

**Returns:**
- `Vector3d`: The gravitational acceleration vector in m/s²

**Exceptions:**
- `ArgumentException`: Thrown when input is invalid (zero, NaN, or infinity vectors)

## Example Usage

```csharp
using FlightCore;

// At Earth surface
var surfacePosition = new Vector3d(6378137.0, 0.0, 0.0);
var surfaceAcceleration = Gravity.Acceleration(surfacePosition);
// Returns approximately (-9.79828548, 0, 0) m/s²

// At 100km altitude
var altitudePosition = new Vector3d(6478137.0, 0.0, 0.0);
var altitudeAcceleration = Gravity.Acceleration(altitudePosition);
// Returns approximately (-9.49811709, 0, 0) m/s²
```

## Implementation Notes

- All calculations use double precision for accuracy
- The library is compatible with .NET Standard 2.1
- Input and output use the same Earth-centered Cartesian axes chosen by the caller. This method performs no frame conversion and adds no centrifugal or Coriolis acceleration.
- Uses the WGS84 gravitational parameter with a spherical point-mass approximation; it does not model oblateness or other gravity harmonics.

Run the console tests with a .NET 9 SDK: `dotnet run --project FlightCore.Tests/FlightCore.Tests.csproj`. The library targets .NET Standard 2.1; Unity integration requires separate verification.
