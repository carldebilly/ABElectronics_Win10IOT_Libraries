﻿using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace ABElectronics_Win10IOT_Libraries
{
    /// <summary>
    /// Class for controlling the Delta-Sigma Pi expansion board from AB Electronics UK
    /// </summary>
    public class DeltaSigmaPi
    {
        private ABE_Helpers helper = new ABE_Helpers();

        private I2cDevice i2cbus1; // i2c bus for ADC chip 1
        private I2cDevice i2cbus2; // i2c bus for ADC chip 2

        /// <summary>
        /// I2C address for the U1 (channels 1 - 4)
        /// </summary>
        public byte Address1 { get; set; }

        /// <summary>
        /// I2C address for the U2 (channels 5 - 8)
        /// </summary>
        public byte Address2 { get; set; }

        /// <summary>
        /// Shows if there is a connection with the ADC Pi
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return isConnected;
            }

            private set
            {
                isConnected = value;
            }
        }



        // internal variables

        private bool isConnected;
        private byte config1 = 0x9C;  // PGAx1, 18 bit, continuous conversion, channel 1
        private byte currentchannel1 = 0;  // channel variable for ADC 1
        private byte config2 = 0x9C;  // PGAx1, 18 bit, continuous-shot conversion, channel 1
        private byte currentchannel2 = 0;  // channel variable for ADC 2
        private byte bitrate = 18;  // current bit rate
        private byte conversionmode = 1; // Conversion Mode
        private double pga = 0.5;  // current PGA setting
        private double lsb = 0.0000078125;  // default LSB value for 18 bit
        private bool signbit = false;

        // create byte array and fill with initial values to define size

        Byte[] __adcreading = { 0, 0, 0, 0 };

        /// <summary>
        /// Instance of a Delta-Sigma Pi bus
        /// </summary>
        /// <param name="i2caddress1">I2C address for the U1 (channels 1 - 4)</param>
        /// /// <param name="i2caddress2">I2C address for the U2 (channels 5 - 8)</param>
        public DeltaSigmaPi(byte i2caddress1 = 0x68, byte i2caddress2 = 0x69)
        {
            Address1 = i2caddress1;
            Address2 = i2caddress2;
            IsConnected = false;
        }

        /// <summary>
        /// Open a connection with the Delta-Sigma Pi
        /// </summary>
        public async Task Connect()
        {
            /* Initialize both I2C busses */
            try
            {
                string aqs = I2cDevice.GetDeviceSelector(helper.I2C_CONTROLLER_NAME);  /* Find the selector string for the I2C bus controller   */
                var dis = await DeviceInformation.FindAllAsync(aqs);            /* Find the I2C bus controller device with our selector string  */

                var settings1 = new I2cConnectionSettings(Address1);
                var settings2 = new I2cConnectionSettings(Address2);

                settings1.BusSpeed = I2cBusSpeed.FastMode;
                settings2.BusSpeed = I2cBusSpeed.FastMode;

                i2cbus1 = await I2cDevice.FromIdAsync(dis[0].Id, settings1);    /* Create an I2cDevice with our selected bus controller and I2C settings */
                i2cbus2 = await I2cDevice.FromIdAsync(dis[0].Id, settings2);    /* Create an I2cDevice with our selected bus controller and I2C settings */

                // check if the i2c busses are not null
                if ((i2cbus1 != null) && (i2cbus2 != null))
                {
                    // set the initial bit rate and trigger a Connected event handler
                    IsConnected = true;
                    SetBitRate(bitrate);

                    EventHandler handler = Connected;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }
                }
                else
                {
                    IsConnected = false;
                }
                return;
            }
            /* If initialization fails, display the exception and stop running */
            catch (Exception e)
            {
                IsConnected = false;
                throw e;
            }
        }

        /// <summary>
        /// Event occurs when connection is made
        /// </summary>
        public event EventHandler Connected;


        /// <summary>
        /// internal method for updating the configuration to the selected channel
        /// </summary>
        /// <param name="channel">ADC channel, 1 - 8</param>
        private void SetChannel(byte channel)
        {
            if ((channel < 5) && (channel != currentchannel1))
            {
                switch (channel)
                {
                    case 1:
                        config1 = helper.UpdateByte(config1, 5, false);
                        config1 = helper.UpdateByte(config1, 6, false);
                        currentchannel1 = 1;
                        break;
                    case 2:
                        config1 = helper.UpdateByte(config1, 5, true);
                        config1 = helper.UpdateByte(config1, 6, false);
                        currentchannel1 = 2;
                        break;
                    case 3:
                        config1 = helper.UpdateByte(config1, 5, false);
                        config1 = helper.UpdateByte(config1, 6, true);
                        currentchannel1 = 3;
                        break;
                    case 4:
                        config1 = helper.UpdateByte(config1, 5, true);
                        config1 = helper.UpdateByte(config1, 6, true);
                        currentchannel1 = 4;
                        break;
                }
            }
            else if ((channel >= 5 && channel <= 8) && (channel != currentchannel2))
            {
                switch (channel)
                {
                    case 5:
                        config2 = helper.UpdateByte(config2, 5, false);
                        config2 = helper.UpdateByte(config2, 6, false);
                        currentchannel2 = 5;
                        break;
                    case 6:
                        config2 = helper.UpdateByte(config2, 5, true);
                        config2 = helper.UpdateByte(config2, 6, false);
                        currentchannel2 = 6;
                        break;
                    case 7:
                        config2 = helper.UpdateByte(config2, 5, false);
                        config2 = helper.UpdateByte(config2, 6, true);
                        currentchannel2 = 7;
                        break;
                    case 8:
                        config2 = helper.UpdateByte(config2, 5, true);
                        config2 = helper.UpdateByte(config2, 6, true);
                        currentchannel2 = 8;
                        break;
                }
            }
        }



        /// <summary>
        /// Returns the voltage from the selected ADC channel
        /// </summary>
        /// <param name="channel">1 to 8</param>
        /// <returns></returns>
        public double ReadVoltage(byte channel)
        {
            int raw = ReadRaw(channel);
            double voltage = 0;

            if (signbit) {
                voltage = (raw * (lsb / (pga))) - (2.048 / (pga * 2));
            }
            else {
                voltage = raw * (lsb / pga);
            }

            return voltage;
        }


        /// <summary>
        /// Reads the raw value from the selected ADC channel
        /// </summary>
        /// <param name="channel">1 to 8</param>
        /// <returns>raw integer value from ADC buffer</returns>
        public int ReadRaw(byte channel)
        {

            byte h = 0;
            byte l = 0;
            byte m = 0;
            byte s = 0;
            byte config = 0;

            int t = 0;
            signbit = false;
            I2cDevice bus;

            SetChannel(channel);

            // get the configuration and i2c bus for the selected channel
            if (channel < 5)
            {
                config = config1;
                bus = i2cbus1;
            }
            else if (channel >= 5 && channel <= 8)
            {
                config = config2;
                bus = i2cbus2;
            }
            else
            {
                throw new NotSupportedException();
            }

            // set the configuration register for the selected channel


            // if the conversion mode is set to one-shot update the ready bit to 1
            if (conversionmode == 0)
            {
                config = helper.UpdateByte(config, 7, true);
                helper.WriteI2CByte(bus, config, 0x00);
                config = helper.UpdateByte(config, 7, false);
            }

            // keep reading the ADC data until the conversion result is ready
            int timeout = 1000; // number of reads before a timeout occurs
            int x = 0;
            do
            {
                if (bitrate == 18)
                {
                    __adcreading = helper.ReadI2CBlockData(bus, config, 4);
                    h = __adcreading[0];
                    m = __adcreading[1];
                    l = __adcreading[2];
                    s = __adcreading[3];
                }
                else
                {
                    __adcreading = helper.ReadI2CBlockData(bus, config, 3);
                    h = __adcreading[0];
                    m = __adcreading[1];
                    s = __adcreading[2];
                }

                // check bit 7 of s to see if the conversion result is ready
                if (!helper.CheckBit(s, 7))
                {
                    break;
                }

                if (x > timeout)
                {
                    // timeout occurred
                    throw new TimeoutException();
                }

                x++;
            } while (true);

            // extract the returned bytes and combine in the correct order
            switch (bitrate)
            {
                case 18:

                    t = ((h & 3) << 16) | (m << 8) | l;
                    signbit = helper.CheckIntBit(t, 17);
                    if (signbit)
                    {
                        t = helper.UpdateInt(t, 17, false);
                    }
                    break;
                case 16:
                    t = (h << 8) | m;
                    signbit = helper.CheckIntBit(t, 15);
                    if (signbit)
                    {
                        t = helper.UpdateInt(t, 15, false);
                    }
                    break;
                case 14:

                    t = ((h & 63) << 8) | m;
                    signbit = helper.CheckIntBit(t, 13);
                    if (signbit)
                    {
                        t = helper.UpdateInt(t, 13, false);
                    }
                    break;
                case 12:
                    t = ((h & 15) << 8) | m;
                    signbit = helper.CheckIntBit(t, 11);
                    if (signbit)
                    {
                        t = helper.UpdateInt(t, 11, false);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return t;
        }

        /// <summary>
        /// Programmable Gain Amplifier gain selection
        /// </summary>
        /// <param name="gain">Set to 1, 2, 4 or 8</param>
        public void SetPGA(byte gain)
        {
            switch (gain)
            {
                case 1:
                    config1 = helper.UpdateByte(config1, 0, false);
                    config1 = helper.UpdateByte(config1, 1, false);
                    config2 = helper.UpdateByte(config2, 0, false);
                    config2 = helper.UpdateByte(config2, 1, false);
                    pga = 0.5;
                    break;
                case 2:
                    config1 = helper.UpdateByte(config1, 0, true);
                    config1 = helper.UpdateByte(config1, 1, false);
                    config2 = helper.UpdateByte(config2, 0, true);
                    config2 = helper.UpdateByte(config2, 1, false);
                    pga = 1;
                    break;
                case 4:
                    config1 = helper.UpdateByte(config1, 0, false);
                    config1 = helper.UpdateByte(config1, 1, true);
                    config2 = helper.UpdateByte(config2, 0, false);
                    config2 = helper.UpdateByte(config2, 1, true);
                    pga = 2;
                    break;
                case 8:
                    config1 = helper.UpdateByte(config1, 0, true);
                    config1 = helper.UpdateByte(config1, 1, true);
                    config2 = helper.UpdateByte(config2, 0, true);
                    config2 = helper.UpdateByte(config2, 1, true);
                    pga = 4;
                    break;
                default:
                    throw new NotSupportedException();
            }
            //helper.WriteI2CSingleByte(i2cbus1, config1);
           // helper.WriteI2CSingleByte(i2cbus2, config2);
        }

        /// <summary>
        /// Set the sample resolution
        /// </summary>
        /// <param name="rate">12 = 12 bit(240SPS max), 14 = 14 bit(60SPS max), 16 = 16 bit(15SPS max), 18 = 18 bit(3.75SPS max)</param>
        public void SetBitRate(byte rate)
        {
            switch (rate)
            {
                case 12:
                    config1 = helper.UpdateByte(config1, 2, false);
                    config1 = helper.UpdateByte(config1, 3, false);
                    config2 = helper.UpdateByte(config2, 2, false);
                    config2 = helper.UpdateByte(config2, 3, false);
                    bitrate = 12;
                    lsb = 0.0005;
                    break;
                case 14:
                    config1 = helper.UpdateByte(config1, 2, true);
                    config1 = helper.UpdateByte(config1, 3, false);
                    config2 = helper.UpdateByte(config2, 2, true);
                    config2 = helper.UpdateByte(config2, 3, false);
                    bitrate = 14;
                    lsb = 0.000125;
                    break;
                case 16:
                    config1 = helper.UpdateByte(config1, 2, false);
                    config1 = helper.UpdateByte(config1, 3, true);
                    config2 = helper.UpdateByte(config2, 2, false);
                    config2 = helper.UpdateByte(config2, 3, true);
                    bitrate = 16;
                    lsb = 0.00003125;
                    break;
                case 18:
                    config1 = helper.UpdateByte(config1, 2, true);
                    config1 = helper.UpdateByte(config1, 3, true);
                    config2 = helper.UpdateByte(config2, 2, true);
                    config2 = helper.UpdateByte(config2, 3, true);
                    bitrate = 18;
                    lsb = 0.0000078125;
                    break;
                default:
                    throw new NotSupportedException();
            }
            //helper.WriteI2CSingleByte(i2cbus1, config1);
            //helper.WriteI2CSingleByte(i2cbus2, config2);
        }

        /// <summary>
        /// Set the conversion mode for ADC
        /// </summary>
        /// <param name="mode">0 = One shot conversion mode, 1 = Continuous conversion mode</param>
        private void SetConversionMode(bool mode)
        {
            if (mode)
            {
                config1 = helper.UpdateByte(config1, 4, true);
                config2 = helper.UpdateByte(config2, 4, true);
                conversionmode = 1;
            }
            else
            {
                config1 = helper.UpdateByte(config1, 4, false);
                config2 = helper.UpdateByte(config2, 4, false);
                conversionmode = 0;
            }
        }

        /// <summary>
        /// Dispose of the DeltaSigmaPi instance.
        /// </summary>
        private void Dispose()
        {
            i2cbus1.Dispose();
            i2cbus2.Dispose();
            IsConnected = false;
        }

    }
}
