# Dynamic image benchmarks (.NET 10)

Run from this directory on Windows x64 with the .NET 10 SDK. Native libraries come from the existing resizer project dependencies. No external image downloads are needed. Other platforms have not been certified for this suite.

```powershell
dotnet build -c Release -m:1
$benchmark = '.\bin\Release\net10.0\Umbrella.DynamicImage.Benchmark.exe'
& $benchmark --list flat

# All resize cases: 144 common-format cases + 16 NetVips AVIF cases.
& $benchmark --filter '*DynamicImageResizerBenchmark*' '*NetVipsAvifBenchmark*'

# Dry smoke check of every resize case (not statistically meaningful timing).
& $benchmark --filter '*DynamicImageResizerBenchmark*' '*NetVipsAvifBenchmark*' --job Dry

# One scenario, all three implementations; default BenchmarkDotNet measurement.
& $benchmark --filter '*DynamicImageResizerBenchmark*1920-jpg-Fit320-Jpeg*'

# AVIF only; add --job Short for a quicker initial comparison.
& $benchmark --filter '*NetVipsAvifBenchmark*'
& $benchmark --filter '*NetVipsAvifBenchmark*1920-jpg-Fit320-Avif*'

# Existing URL parsing benchmark.
& $benchmark --filter '*DynamicImageUtilityBenchmark*'

# Isolated process-memory measurements (defaults: all cases, three repetitions).
& $benchmark --process-memory
& $benchmark --process-memory --scenario '1920-jpg-Fit320-Jpeg'
& $benchmark --process-memory --implementation NetVips --scenario '*-Avif'
& $benchmark --process-memory --implementation NetVips --scenario '1920-jpg-Fit320-Avif' --timeout-seconds 600
```

BenchmarkDotNet uses the current .NET 10 runtime and its standard out-of-process job unless `--job` selects Dry or Short. Do not override the runtime or use in-process execution for comparisons. Default Markdown/CSV reports and detailed logs are written under `BenchmarkDotNet.Artifacts`, which is ignored by Git. Use a quiet machine, Release builds, and the same hardware/runtime when comparing results. Full runs, particularly AVIF, can take substantial time.

## Workloads

Four fixtures (JPEG/opaque PNG at 1920×1080 and 3840×2160) are each resized using Fit320, Fit1280, Crop320, and Focal320. Fit uses `ScaleDown`; the 320×320 fit produces 320×180, and Fit1280 produces 1280×720. Crops produce 320×320, with Focal320 anchored at (0.25, 0.75). All calls explicitly request Medium filtering and quality 75.

JPEG, PNG, and WebP have three implementation methods; SkiaSharp is the ratio baseline within each scenario. AVIF has a separate NetVips-only class with absolute metrics and no cross-implementation ratio. FreeImage explicitly rejects AVIF; SkiaSharp reports it unsupported. Neither is silently skipped or represented with a zero AVIF result. AVIF input is outside this suite.

Each measured call includes decoding, resizing/cropping, encoding, and creation of the returned byte array. Input reading, resizer construction, logging, and validation are outside measurement. Setup checks nonempty output, encoded signature (including AVIF file-type brand), reported dimensions, and dimensions after decoding with the selected backend. Validation failures terminate the case. Output byte counts are in setup logs and process-memory reports.

Each process constructs only its selected backend. NetVips operation caching is disabled (`Cache.Max = 0`) to avoid reuse of processing for identical inputs. Native threading defaults are preserved; NetVips's effective concurrency setting is logged. The other backends expose no threading setting in these wrappers. BenchmarkDotNet also records the runtime and host environment.

These compare the wrappers as implemented, not equivalent visual quality. FreeImage currently always uses Lanczos3, fixed JPEG quality flags, best PNG compression, and default WebP flags. NetVips and SkiaSharp map the requested settings differently. PNG quality is not a lossy-quality control. Output sizes help contextualize timing but are not a quality score. There are no pass/fail performance thresholds.

## Reading memory results

BenchmarkDotNet `Allocated` is managed bytes allocated per operation; GC columns show collection activity. Native pixel buffers and native codec allocations are not included.

`--process-memory` is a separate footprint measurement, not a timing benchmark or a native-allocation profiler. For each supported implementation/scenario/repetition, it launches a fresh worker, loads input, validates the first resize, and completes two additional warm-ups. It collects garbage once, waits for pending finalizers, and signals readiness. The parent records a baseline, starts 20 sequential resizes, and samples refreshed working-set/private-byte counters at a target interval of 10 ms. Outputs are consumed and released after each call. The worker stays alive until the parent captures final counters and acknowledges completion.

Reports include baseline, sampled maximum, and end values for working set and private bytes, plus lifetime peak working set, output bytes, sample count, and worker PID. These include native memory, managed heap capacity, loaded libraries, and runtime overhead. Private bytes and resident working set measure different things. Short peaks can be missed by sampling; lifetime peak includes startup, validation, and warm-up, and cannot be interpreted as the resize loop's isolated peak. No GC is forced during or after the measured loop.

Raw CSV/Markdown, median summary CSV/Markdown, and per-worker logs go under `BenchmarkDotNet.Artifacts/process-memory/<timestamp>`. Medians use successful runs only and show success/failure counts. Failed measurements are identified by status; their partial counters are not usable results. Timeouts and encoder/native-library errors yield a nonzero exit code. The runner continues other cases and writes reports after each worker.

Memory options accept `--scenario` and `--implementation` wildcard filters (`*`, `?`), `--repetitions` (default 3), `--timeout-seconds` (default 600 per worker including setup), and `--output` (report directory). No matches or invalid options fail. For a quick harness check use `--repetitions 1`; use the default three for comparisons. To check timeout reporting, use `--timeout-seconds 0.001` with one selected case. Missing fixtures and unavailable encoders fail visibly in worker logs.

## Fixture provenance and regeneration

The checked-in fixtures are original synthetic test patterns, generated by `FixtureGenerator.cs`; no third-party photographs or licenses are involved. The recipe uses integer gradients, checkerboard edges, diagonal detail, and xorshift32 texture seeded with `0x12345678` for each size. Every pixel is opaque. JPEG uses quality 90; PNG is lossless. They exercise detailed image content but do not represent every photographic workload.

Regenerate only when intentionally changing the corpus, from this directory:

```powershell
& $benchmark --generate-fixtures '.\Fixtures'
dotnet build -c Release -m:1
```

Generation is an offline mode, never part of benchmark setup or memory workers. Encoded bytes can change with SkiaSharp/native codec versions; use the checked-in files for reproducible comparisons, and regenerate with the repository's pinned dependencies. Fixtures are copied to the build output and BenchmarkDotNet worker output by MSBuild.
