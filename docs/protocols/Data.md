# Data socket reference

This document lists the modes & formats supported by the data socket (typically port 5026).

## Tag credit mode

Send `T` on the socket to enable tag credit mode.

Uses credit mechanism to request data.

### Header

```
[tag u32][payload length u64][payload]
```

Implemented tags: 
``` 
DATA
DBUG
```

### DATA v1

```
u8 version;
u32 seqnum;
u16 numChannels;
u64 fsPerSample;
i64 triggerFs;
f64 hwWaveformsPerSec;
[channel header & channel data array]
```

```
version: 1
```

### DBUG v1

```
u8 version;
u8 level;
u8[] message;
```

```
version: 1
level:  0 trace, 1 debug, 2 information, 3 warning, 4 error, 5 critical
message: ASCII, not null-terminated (header payload length value determines length of string)
```

