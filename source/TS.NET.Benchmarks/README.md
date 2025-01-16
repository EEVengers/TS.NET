## ShuffleI8.FourChannels (1006632960 samples)

| Platform                                   | Mean      | Error    | StdDev   |
|------------------------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB DDR5 4800MT/s [scalar] | 232.36 ms | 0.100 ms | 0.089 ms |
| x64 i7-13700H 2x8GB DDR5 4800MT/s [SSSE3]  |  41.67 ms | 0.459 ms | 0.430 ms |
| x64 i7-13700H 2x8GB DDR5 4800MT/s [AVX2]   |  36.39 ms | 0.388 ms | 0.344 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [scalar]    | 384.34 ms | 0.523 ms | 0.489 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [SSSE3]     |  21.93 ms | 0.437 ms | 0.680 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [AVX2]      |  16.90 ms | 0.153 ms | 0.128 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [scalar]  | 203.18 ms | 0.937 ms | 1.590 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [SSSE3]   |  17.91 ms | 0.091 ms | 0.081 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [AVX2]    |  18.04 ms | 0.079 ms | 0.070 ms |
| ARM64 M4 4P6E 16GB [scalar]                | 127.49 ms | 0.755 ms | 0.706 ms |
| ARM64 M4 4P6E 16GB [Neon]                  |  18.67 ms | 0.108 ms | 0.101 ms |

## ShuffleI8.TwoChannels (1006632960 samples)

| Platform                                   | Mean      | Error    | StdDev   |
|------------------------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB DDR5 4800MT/s [scalar] | 239.20 ms | 0.139 ms | 0.130 ms |
| x64 i7-13700H 2x8GB DDR5 4800MT/s [SSSE3]  |  38.80 ms | 0.235 ms | 0.220 ms |
| x64 i7-13700H 2x8GB DDR5 4800MT/s [AVX2]   |  35.54 ms | 0.302 ms | 0.268 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [scalar]    | 479.38 ms | 1.066 ms | 0.890 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [SSSE3]     |  18.07 ms | 0.341 ms | 0.511 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [AVX2]      |  15.53 ms | 0.294 ms | 0.315 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [scalar]  | 209.88 ms | 3.152 ms | 3.372 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [SSSE3]   |  16.99 ms | 0.086 ms | 0.080 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [AVX2]    |  16.05 ms | 0.085 ms | 0.075 ms |
| ARM64 M4 4P6E 16GB [scalar]                | 128.47 ms | 0.548 ms | 0.486 ms |
| ARM64 M4 4P6E 16GB [Neon]                  |  17.03 ms | 0.065 ms | 0.058 ms |

## RisingEdgeTriggerI8 - 0% trigger throughput (1006632960 samples)

| Platform                     | Mean      | Error    | StdDev   |
|----------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar] | 283.18 ms | 0.334 ms | 0.313 ms |
| x64 i7-13700H 2x8GB [AVX2]   |  15.66 ms | 0.014 ms | 0.013 ms |
| ARM64 M4 4P6E 16GB [scalar]  | 233.63 ms | 1.485 ms | 1.389 ms |
| ARM64 M4 4P6E 16GB [Neon]    |  15.77 ms | 0.045 ms | 0.040 ms |

## RisingEdgeTriggerI8 - 48% trigger throughput (1006632960 samples)

| Platform                     | Mean      | Error    | StdDev   |
|----------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar] | 149.93 ms | 0.182 ms | 0.170 ms |
| x64 i7-13700H 2x8GB [AVX2]   |   9.56 ms | 0.022 ms | 0.020 ms |
| ARM64 M4 4P6E 16GB [scalar]  | 123.59 ms | 0.458 ms | 0.406 ms |
| ARM64 M4 4P6E 16GB [Neon]    |   8.90 ms | 0.054 ms | 0.051 ms |

## RisingEdgeTriggerI8 - 100% trigger throughput (1006632960 samples)

| Platform                     | Mean      | Error    | StdDev   |
|----------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar] |   0.77 ms | 0.000 ms | 0.000 ms |
| x64 i7-13700H 2x8GB [AVX2]   |   0.77 ms | 0.000 ms | 0.000 ms |
| ARM64 M4 4P6E 16GB [scalar]  |   0.87 ms | 0.024 ms | 0.070 ms |
| ARM64 M4 4P6E 16GB [Neon]    |   0.88 ms | 0.022 ms | 0.066 ms |

## FallingEdgeTriggerI8 - 0% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar]  |   ms |  ms |  ms |
| x64 i7-13700H 2x8GB [AVX2]    |   ms |  ms |  ms |
| ARM64 M4 4P6E 16GB [scalar]   | 231.36 ms | 1.308 ms | 1.160 ms |
| ARM64 M4 4P6E 16GB [Neon]     |  15.70 ms | 0.057 ms | 0.044 ms |

## FallingEdgeTriggerI8 - 48% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar]  |   ms |  ms |  ms |
| x64 i7-13700H 2x8GB [AVX2]    |   ms |  ms |  ms |
| ARM64 M4 4P6E 16GB [scalar]   | 122.49 ms | 0.742 ms | 0.694 ms |
| ARM64 M4 4P6E 16GB [Neon]     |   8.50 ms | 0.040 ms | 0.037 ms |

## FallingEdgeTriggerI8 - 100% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar]  |    ms |  ms |  ms |
| x64 i7-13700H 2x8GB [AVX2]    |    ms |  ms |  ms |
| ARM64 M4 4P6E 16GB [scalar]   |   0.78 ms | 0.022 ms | 0.065 ms |
| ARM64 M4 4P6E 16GB [Neon]     |   0.83 ms | 0.020 ms | 0.058 ms |

## Links

https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/viewing-jit-dumps.md  
https://github.com/dotnet/runtime/blob/main/src/coreclr/inc/clrconfigvalues.h#L718  
https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html  
https://developer.arm.com/architectures/instruction-sets/intrinsics/  

## ASM

### ShuffleI8.FourChannels x64 [AVX2] hot loop
```
vmovups  ymm2, ymmword ptr [rax]
vmovups  ymm3, ymmword ptr [r11]
vpshufb  ymm3, ymm3, ymm0
vpshufb  ymm2, ymm2, ymm0
vpermd   ymm2, ymm1, ymm2
vpermd   ymm3, ymm1, ymm3
vpunpckhqdq ymm4, ymm2, ymm3
vpunpcklqdq ymm2, ymm2, ymm3
vmovaps  ymm3, ymm2
vmovups  xmmword ptr [r8], xmm3
vmovaps  ymm3, ymm4
vmovups  xmmword ptr [r8+8*r10], xmm3
vextracti128 xmm2, ymm2, 1
vmovups  xmmword ptr [r8+8*r9], xmm2
vextracti128 xmm2, ymm4, 1
vmovups  xmmword ptr [r8+8*rcx], xmm2
add      rax, 64
add      r11, 64
add      r8, 16
cmp      rax, rdx
```

### ShuffleI8.TwoChannels x64 [AVX2] hot loop
```
vmovups  ymm0, ymmword ptr [rax]
vpshufb  ymm0, ymm0, ymmword ptr [reloc @RWD00]
vmovups  ymm1, ymmword ptr [reloc @RWD32]
vpermd   ymm0, ymm1, ymm0
vmovd    qword ptr [r8], xmm0
vmovaps  ymm1, ymm0
vpextrq  qword ptr [r8+0x08], xmm1, 1
vextracti128 xmm1, ymm0, 1
vmovd    qword ptr [r8+8*r10], xmm1
vextracti128 xmm0, ymm0, 1
vpextrq  qword ptr [r8+8*rcx], xmm0, 1
add      rax, 32
add      r8, 16
cmp      rax, rdx
```