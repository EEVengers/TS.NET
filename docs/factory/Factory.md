# Factory

## Testbench software configuration
To show factory sequences in the testbench software, edit `variables.json` to display the factory sequences:
```"sequenceTypes": [ "factory" ]```

### Factory bring-up sequence

Requires a JTAG-HS2 USB device.  
Requires the `FactoryHwid.csv` file alongside the TS.NET.Testbench.UI executable, with the `Serial`, `Board revision` and `Build configuration` columns correctly populated. The remaining two columns have placeholder `-` values.

