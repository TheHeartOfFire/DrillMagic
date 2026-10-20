# Drill Magic

Drill Magic is a professional desktop graphics utility built with WinUI 3 and .NET designed to generate print-ready diamond painting canvases. The application provides an end-to-end production pipeline that programmatically standardizes arbitrary source images into physical templates, allowing creators to manage the entire artistic process from initial concept to physical execution.

## ✨ Features & User Experience
* **Automated Canvas Generation:** Instantly convert any standard image into a structured diamond painting grid layout.
* **Granular Grid Editing:** A responsive workspace allowing users to edit individual image chunks, adjust target color counts, and fine-tune details before printing.
* **Print-Ready Vector Export:** Export final canvas maps into structured, high-resolution multi-page PDFs tailored for transfer to physical mediums.
* **Modern Interface:** Full native support for Windows 11 design paradigms, including seamless Light and Dark mode preferences.

## 🛠️ Architecture & Technical Stack
For technical reviewers, the codebase strictly adheres to enterprise-grade desktop patterns to guarantee decoupling, modularity, and smooth scaling.

* **UI Framework:** WinUI 3 utilizing a component-driven presentation layer.
* **Design Pattern:** Deep decoupling achieved via the **MVVM (Model-View-ViewModel)** architecture.
* **Image Processing Subsystem:** Custom color-distance mapping algorithms using channel-wise averaging and Euclidean color-distance calculations to map raw pixels to constrained target palettes.
* **Deployment Pipeline:** Integrated with the **Windows Store Partner Center** to manage active beta testing lifecycles and build distribution.

## 💻 Getting Started

### Beta Access & Installation
Drill Magic is currently in a managed closed-beta testing phase via the Microsoft Store:
1. Accepted beta testers can download the package directly from their Windows Store library.
2. Updates are delivered automatically through the secure Microsoft Store deployment pipeline.

### Compiling from Source
To clone and compile this project locally, you will need:
* **Visual Studio 2022** (with `.NET desktop development` workload enabled)
* **.NET 9 SDK** or higher

```bash
git clone https://github.com/TheHeartOfFire/DrillMagic
```
1. Open the solution file (.sln) in Visual Studio.

2. Restore required NuGet dependencies and build in Release mode.
