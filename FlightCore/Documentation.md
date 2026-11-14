# FlightCore Gravity Library Documentation

## Project Structure

```
FlightCore/
├── Gravity.cs              # Main implementation
├── Vector3d.cs             # 3D vector class
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

### Vector3d Class

A simple 3D vector implementation with:
- X, Y, Z components
- Magnitude() method
- Proper equality and hashing support

## Mathematical Implementation

The library implements Newton's law of universal gravitation:

**a** = -μ × **r** / |**r**|³

Where:
- **a** is the acceleration vector (m/s²)
- μ is Earth's gravitational parameter (3.986004418 × 10¹⁴ m³/s²)  
- **r** is the position vector from Earth's center (m)
- |**r**| is the magnitude of the position vector

## Validation and Testing

The implementation has been rigorously tested with:

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
   - NaN and infinity values are properly handled

## Compatibility

- Target Framework: .NET Standard 2.1
- Uses only standard library types
- No external dependencies
- Compatible with Unity projects using URP

## Performance Considerations

- All calculations use double precision for accuracy
- Minimal memory allocation during computation
- Optimized for real-time simulation scenarios
- Vector operations are computed inline without intermediate objects