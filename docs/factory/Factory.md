# Factory

## Testbench software configuration
To show factory sequences in the testbench software, edit `variables.json` to display the factory sequences:
```"sequenceTypes": [ "factory" ]```

### Factory bring-up sequence

Requires a JTAG-HS2 USB device.  

Requires the `FactoryHwid.csv` file alongside the TS.NET.Testbench.UI executable, with the `Serial`, `Board revision` and `Build configuration` columns correctly populated. The remaining two columns have placeholder `-` values.  

Go to the `source/TS.NET.JTAG/Bitfiles` directory and download the files listed in `README.md` before building the Testbench.

When running the sequence, the `HWID input` step will bring up a dialog. Entering the serial number (manually or with scanner) and pressing Enter will trigger a search of the FactoryHwid.csv file for a serial number match to populate the other fields.
