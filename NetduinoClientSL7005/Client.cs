//---------------------------------------------------------------------------------
// Copyright (c) 2017, devMobile Software
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//---------------------------------------------------------------------------------
using System;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;
using devMobile.NetMF.Sensor;
using Gralin.NETMF.Nordic;
using SecretLabs.NETMF.Hardware.Netduino;


namespace devMobile.IoT.FIeldGateway.Netduino.Client
{
   class Client
   {
      private const byte nRF24Channel = 15;
      private const NRFDataRate nRF24DataRate = NRFDataRate.DR250kbps;
      private readonly byte[] nRF24ClientAddress = Encoding.UTF8.GetBytes("T&H01");
      private readonly byte[] nRF24BaseStationAddress = Encoding.UTF8.GetBytes("Base1");
      private static byte[] deviceIdentifier;
      private readonly OutputPort led = new OutputPort(Pins.ONBOARD_LED, false);
      private readonly NRF24L01Plus radio;
      private readonly SiliconLabsSI7005 sensor = new SiliconLabsSI7005();

      
      public Client()
      {
         radio = new NRF24L01Plus();
      }


      public void Run()
      {
         // Configure the nRF24 hardware
         radio.OnDataReceived += OnReceive;
         radio.OnTransmitFailed += OnSendFailure;
         radio.OnTransmitSuccess += OnSendSuccess;

         radio.Initialize(SPI.SPI_module.SPI1, Pins.GPIO_PIN_D7, Pins.GPIO_PIN_D3, Pins.GPIO_PIN_D2);
         radio.Configure(nRF24ClientAddress, nRF24Channel, nRF24DataRate);
         radio.Enable();

         // Setup the device unique identifer, in this case the hardware MacAddress
         deviceIdentifier = NetworkInterface.GetAllNetworkInterfaces()[0].PhysicalAddress;
         Debug.Print(" Device Identifier : " + BytesToHexString(deviceIdentifier));

         Timer humidityAndtemperatureUpdates = new Timer(HumidityAndTemperatureTimerProc, null, 15000, 15000);

         Thread.Sleep(Timeout.Infinite);
      }


      private void HumidityAndTemperatureTimerProc(object state)
      {
         led.Write(true);

         double humidity = sensor.Humidity();
         double temperature = sensor.Temperature();

         Debug.Print("H:" + humidity.ToString("F1") + " T:" + temperature.ToString("F1"));
         string values = "T " + temperature.ToString("F1") + ",H " + humidity.ToString("F0");

         // Stuff the 2 byte header ( payload type & deviceIdentifierLength ) + deviceIdentifier into payload
         byte[] payload = new byte[ 1 + deviceIdentifier.Length + values.Length];
         payload[0] =  (byte)((1 << 4) | deviceIdentifier.Length );
         Array.Copy(deviceIdentifier, 0, payload, 1, deviceIdentifier.Length);

         Encoding.UTF8.GetBytes(values, 0, values.Length, payload, deviceIdentifier.Length + 1 );

         radio.SendTo(nRF24BaseStationAddress, payload );
      }

      
      private void OnSendSuccess()
      {
         led.Write(false);

         Debug.Print("Send Success!");
      }


      private void OnSendFailure()
      {
         Debug.Print("Send failed!");
      }


      private void OnReceive(byte[] data)
      {
         led.Write(!led.Read());

         string message = new String(Encoding.UTF8.GetChars(data));

         Debug.Print("Receive " + message); ;
      }
      
      
      private static string BytesToHexString(byte[] bytes)
      {
         string hexString = string.Empty;

         // Create a character array for hexidecimal conversion.
         const string hexChars = "0123456789ABCDEF";

         // Loop through the bytes.
         for (byte b = 0; b < bytes.Length; b++)
         {
            if (b > 0)
               hexString += "-";

            // Grab the top 4 bits and append the hex equivalent to the return string.        
            hexString += hexChars[bytes[b] >> 4];

            // Mask off the upper 4 bits to get the rest of it.
            hexString += hexChars[bytes[b] & 0x0F];
         }

         return hexString;
      }
   }
}