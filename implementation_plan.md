# Implementation Plan

This plan outlines the creation of a pure C# gravity library for the FlightCore component as specified in the requirements. The goal is to implement a minimal, dependency-free library that computes Earth-centered gravitational acceleration using Newton's point-mass model.

## Types

- Create an immutable three-component double vector type (Vector3d) with proper validation
- Implement a public Earth gravity function with the specified gravitational parameter μ = 3.986004418e14 m³/s²
- Add appropriate error handling for invalid inputs (zero vector, NaN, infinity)

## Files

### New Files:
1. `FlightCore/FlightCore.csproj` - .NET Standard 2.1 library project file
2. `FlightCore/Vector3d.cs` - Immutable three-component double vector implementation
3. `FlightCore/Gravity.cs` - Earth gravity calculation function 
4. `FlightCore.Tests/FlightCore.Tests.csproj` - Console test project
5. `FlightCore.Tests/Program.cs` - Test runner with verification of specified cases

### Existing Files to Modify:
- None

## Functions

### New Functions:
1. `Vector3d operator +(Vector3d a, Vector3d b)` - Vector addition
2. `Vector3d operator -(Vector3d a, Vector3d b)` - Vector subtraction  
3. `Vector3d operator *(double scalar, Vector3d vector)` - Scalar multiplication
4. `Vector3d operator *(Vector3d vector, double scalar)` - Scalar multiplication (commutative)
5. `double Magnitude()` - Computes the magnitude of the vector
6. `Vector3d Normalize()` - Returns a normalized version of the vector
7. `static Vector3d Zero` - Constant zero vector
8. `static Vector3d Gravity(Vector3d position)` - Main gravity function computing acceleration using a = -μr / |r|³

## Classes

### New Classes:
1. `Vector3d` - Immutable three-component double vector class with validation
2. `Gravity` - Static class containing the Earth gravity calculation method

## Dependencies

- .NET Standard 2.1 target framework
- No external dependencies (dependency-free as required)
- Uses only standard .NET libraries for math operations and validation

## Testing

### Test Cases:
1. At (6378137, 0, 0) meters, acceleration should be approximately (-9.79828548, 0, 0) m/s²
2. At an off-axis position, acceleration should point directly toward Earth's center  
3. Doubling the distance should reduce acceleration magnitude to one quarter
4. Zero, NaN, and infinity inputs should be rejected with appropriate exceptions

### Test Runner:
- Console application that exits with non-zero code on test failure
- Uses Assert-style validation with descriptive error messages
- Documents numerical tolerances in comments

## Implementation Order

1. Create FlightCore project structure and .csproj file
2. Implement Vector3d immutable vector class with all required operations
3. Implement Gravity static class with main gravity function
4. Create FlightCore.Tests project structure and .csproj file  
5. Implement test cases verifying the specified requirements
6. Validate tests run correctly and exit appropriately
