# FlightCore Gravity Library Documentation

## Project Structure

```
FlightCore/
├── Gravity.cs              # Main implementation
├── Vector3d.cs             # 3D vector struct
├── README.md               # Usage documentation
└── Documentation.md        # This file
```

## Core Components

### Gravity Class

The `Gravity` class is the main entry point for gravitational calculations.

**Static Method:**
- `Acceleration(Vector3d position)` - Calculates gravitational acceleration at given position

**Constants:**
- `EarthGravitationalParameter` - Standard gravitational parameter for Earth (3.986004418 × 10¹⁴ m³/s²)

### Vector3d Struct

An immutable double-precision 3D vector struct with:
- X, Y, Z components
- Magnitude() method
- Normalize(), addition, subtraction, and scalar multiplication
- Constructor validation rejecting NaN and infinity components

## Mathematical Implementation

The library implements Newton's law of universal gravitation:

**a** = -μ × **r** / |**r**|³

Where:
- **a** is the acceleration vector (m/s²)
- μ is Earth's gravitational parameter (3.986004418 × 10¹⁴ m³/s²)  
- **r** is the position vector from Earth's center (m)
- |**r**| is the magnitude of the position vector

## Validation and Testing

The implementation includes focused console tests for:

1. **Surface Position Test**: 
   - Input: (6378137, 0, 0) meters
   - Expected Output: (-9.79828548, 0, 0) m/s²

2. **Off-Axis Test**:
   - Verifies that acceleration vectors point directly toward Earth center
   - Confirms antiparallel relationship between position and acceleration vectors

3. **Distance-Doubling Rule**:
   - Validates inverse square law behavior
   - When distance doubles, acceleration decreases by factor of 4

4. **Invalid Input Handling**:
   - Zero vector inputs are rejected with ArgumentException
   - Vector construction rejects NaN and both infinities in each component

5. **Additional Known Values and Symmetry**:
   - The 100 km example and all six signed coordinate axes are checked
   - Numerical assertions explicitly reject nonfinite results

## Compatibility

- Target Framework: .NET Standard 2.1
- Uses only standard library types
- No external dependencies
- Unity integration and packaged execution require separate verification; URP is independent of the flight library

## Performance Considerations

- All calculations use double precision for accuracy
- Minimal memory allocation during computation
- No performance measurements are established by these tests
- Vector operations return value types; compiler inlining is not guaranteed