# vOptimizer

**vOptimizer** is a lightweight, standalone Windows system optimization utility designed for Gamers and Power Users. It optimizes hardware limits (TDP, Undervolting), adjusts Windows Registry latency parameters, purges system RAM caches, and binds game processes to physical cores dynamically.

Unlike static tuning tools, vOptimizer includes a **closed-loop feedback loop**: it benchmarks your system, applies changes, benchmarks again, and automatically calibrates to the most efficient and stable performance profile.

---

## Key Features

1. **AI Smart Auto-Tuner & Calibration:** Runs a short benchmark to profile your system's heat dissipation rate and automatically recommends the safest, highest-performing settings.
2. **Built-in Benchmark Engine:** Measures CPU and GPU compute capacity (GOPs/GFLOPS) and thermal throttling behaviors directly inside the app.
3. **Before vs. After Comparison Dashboard:** Visualizes your benchmark scores, peak temperatures, power draw, and efficiency metrics side-by-side.
4. **Intel & AMD Undervolting:**
   * **Intel:** Direct writes to MSR `0x150` (FIVR) to undervolt CPU Core, Cache, iGPU, and System Agent.
   * **AMD:** Dynamic Curve Optimizer offsets and custom TDP/VRM limits.
5. **Low-Level OS Latency Tweaks:** Disables network throttling, disables Nagle's TCP buffering algorithm for low-ping gaming, and maximizes system responsiveness.
6. **Dynamic RAM & Process Optimizer:**
   * Clears Windows Standby List and empties working sets of idle background apps on game startup.
   * Ghims game threads to physical CPU cores (Affinity) and raises process priority class to High.

---

## Architecture

vOptimizer is built using **C# .NET 8.0-windows / .NET 9.0-windows** with a modern Fluent design using the **WPF-UI** library. It runs as a single elevated process (`requireAdministrator`) and leverages the signed kernel driver `WinRing0x64.sys` for low-level register access.

---

## Development Roadmap

* **Phase 1:** Repository setup, core libraries, and project structure (Completed).
* **Phase 2:** Low-level register drivers (WinRing0) & Voltage control implementation.
* **Phase 3:** OS Registry Tweaks & RAM purging logic.
* **Phase 4:** Benchmark engine & Auto-calibration closed-loop logic.
* **Phase 5:** WPF Fluent Dashboard UI & System Tray integration.
