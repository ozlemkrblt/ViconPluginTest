# 🖐️ XRHands Integration with Vicon Nexus – Experimental Branch

## 📌 Overview

This branch explores ways to **leverage Unity’s XR Hands package**, particularly the `XRHandSubsystem`, to integrate some of its functionality with a **custom motion tracking setup powered by Vicon Nexus**.

The main goal is to experiment with combining Unity’s built-in hand tracking interfaces—originally designed for XR devices like the Meta Quest—with **marker-based motion capture data** coming from Vicon. This could enable high-fidelity skeletal hand interactions in VR or AR experiences while maintaining the flexibility of external tracking systems.

---

## 🚀 Goals

* Interface Vicon-captured hand data with Unity's `XRHandSubsystem`.
* Reuse Unity’s `XRHand`, `XRHandJoint`, and related logic for rendering, animation, or physics.
* Extend XR-compatible input handling to Vicon-driven skeletons.
* Prototype a hybrid approach where external hand tracking (Vicon) can substitute or override XR device input.

---

## 🧱 About `XRHandSubsystem`

The `XRHandSubsystem` is part of Unity's [XR Hands](https://docs.unity3d.com/Packages/com.unity.xr.hands@latest) package. It provides a platform-agnostic abstraction for hand tracking, with implementations for devices like Meta Quest.

### Key Components

* **`XRHandSubsystem`**
  Core subsystem responsible for updating hand data (left/right) each frame.

* **`XRHand`**
  Represents a full hand (left or right), including its joints and tracking state.

* **`XRHandJoint`**
  Represents an individual joint (e.g., wrist, index tip) with position, rotation, radius, and tracking status.

* **`XRHandTrackingEvents` / `XRHandProviderUtility`**
  Interfaces and utilities for injecting or modifying hand data at runtime.

---

## 🔧 Integration Strategy

The proposed integration involves:

1. **Custom XR Provider (optional)**
   Extending or mocking an `XRHandSubsystem` provider to inject Vicon-driven hand joint data directly into the XRHands system.

2. **Mapping Vicon Joint Data → `XRHandJoint`s**
   Translating Vicon segment transforms into Unity joint poses compatible with XRHands.

3. **Live Update Pipeline**
   Syncing `XRHand` data every frame based on Vicon's streaming output (via DataStream SDK).

4. **Fallback / Override Logic**
   Allowing Vicon data to override default XR hand tracking if a headset is connected but not used for hand tracking.

---

## 🧪 Status

✅ Initial experiments in progress
🔲 Full mapping from Vicon segments to XRHandJoints
🔲 Compatibility test with Unity’s XR Interaction Toolkit
🔲 VR visualization using XR Hands with Vicon as the backend

---

## 📁 Related Files

* `XRHandViconBridge.cs`: (WIP) Script that maps Vicon hand data to XRHand-compatible format.
* `SubjectScript.cs`: Handles per-frame Vicon data updates for each tracked segment.
* `XRHandAdapter`: Potential utility for structuring joint names and mappings.

---

## 📚 Useful Links

* [XR Hands Unity Documentation](https://docs.unity3d.com/Packages/com.unity.xr.hands@latest)
* [XR Subsystems Overview](https://docs.unity3d.com/Manual/xr-subsystems.html)
* [Vicon DataStream SDK](https://docs.vicon.com/display/Nexus)
* [Vicon + Unity Integration Guide (internal)](link-to-your-doc)

