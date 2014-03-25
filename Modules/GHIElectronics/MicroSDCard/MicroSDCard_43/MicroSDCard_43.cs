﻿using System;

using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using GTI = Gadgeteer.SocketInterfaces;

using System.Threading;
using Microsoft.SPOT.IO;

namespace Gadgeteer.Modules.GHIElectronics
{
    // -- CHANGE FOR MICRO FRAMEWORK 4.2 --
    // If you want to use Serial, SPI, or DaisyLink (which includes GTI.SoftwareI2CBus), you must do a few more steps
    // since these have been moved to separate assemblies for NETMF 4.2 (to reduce the minimum memory footprint of Gadgeteer)
    // 1) add a reference to the assembly (named Gadgeteer.[interfacename])
    // 2) in GadgeteerHardware.xml, uncomment the lines under <Assemblies> so that end user apps using this module also add a reference.

    /// <summary>
    /// Represents a slot for Secure Digital (SD) non-volatile memory card.
    /// </summary>
    public class MicroSDCard : GTM.Module
    {
        /// <summary>
        /// Gets a value that indicates whether a memory card is
        /// inserted into the <see cref="MicroSDCard"/>, and the file system
        /// associated with the memory card is mounted.
        /// </summary>
        public bool IsCardMounted { get; private set; }

        private GTI.InterruptInput _MicroSDCardDetect;

        //private PersistentStorage _storage;
        private StorageDevice _device;

        // Note: A constructor summary is auto-generated by the doc builder.
        /// <summary></summary>
        /// <param name="socketNumber">The mainboard socket that has the module plugged into it.</param>
        public MicroSDCard(int socketNumber)
        {
            Socket socket = Socket.GetSocket(socketNumber, true, this, null);
            socket.EnsureTypeIsSupported('F', this);

            // add Insert/Eject events
            RemovableMedia.Insert += Insert;
            RemovableMedia.Eject += Eject;

            IsCardMounted = false;

            _MicroSDCardDetect = GTI.InterruptInputFactory.Create(socket, Socket.Pin.Three, GTI.GlitchFilterMode.On, GTI.ResistorMode.PullUp, GTI.InterruptMode.RisingAndFallingEdge, this);
            _MicroSDCardDetect.Interrupt += (_MicroSDCardDetect_Interrupt);

            try
            {
                // Reserve the additional pins used by the SD interface (pin 3 is automatically reserved by initializing the InterruptInput)
                socket.ReservePin(Socket.Pin.Four, this);
                socket.ReservePin(Socket.Pin.Five, this);
                socket.ReservePin(Socket.Pin.Six, this);
                socket.ReservePin(Socket.Pin.Seven, this);
                socket.ReservePin(Socket.Pin.Eight, this);
                socket.ReservePin(Socket.Pin.Nine, this);

                if (IsCardInserted)
                {
                    MountMicroSDCard();
                }
            }

            catch (Exception e)
            {
                throw new GT.Socket.InvalidSocketException("There is an issue connecting the SD Card module to socket " + socketNumber +
                    ". Please check that all modules are connected to the correct sockets or try connecting the SD Card to a different 'F' socket", e);
            }

        }

        /// <summary>
        /// True if an SD card is inserted
        /// </summary>
        public bool IsCardInserted
        {
            get { return !_MicroSDCardDetect.Read(); }
        }

        /// <summary>
        /// Gets the StorageDevice instance for the currently mounted SD Card
        /// </summary>
        /// <returns>If an SD cards is mounted, returns the StorageDevice instance for the card.  Returns null otherwise.</returns>
        public StorageDevice GetStorageDevice()
        {
            return _device;
        }

        void _MicroSDCardDetect_Interrupt(GTI.InterruptInput sender, bool value)
        {
            Thread.Sleep(500);

            if (IsCardInserted)
            {
                MountMicroSDCard();
            }
            else
            {
                UnmountMicroSDCard();
            }
        }

        /// <summary>
        /// Attempts to mount the file system of a non-volatile memory card
        /// and create a <see cref="T:Microsoft.Gadgeteer.StorageDevice"/> object
        /// associated with the card.
        /// </summary>
        /// <remarks>
        /// <para>
        ///  Use <see cref="MountMicroSDCard"/> and <see cref="UnmountMicroSDCard"/>
        ///  to manually mount and dismount the file system on the non-volatile memory card.
        /// </para>
        /// <para>
        ///  If you call this method when there is no memory card inserted into the 
        ///  slot, or the card is already mounted, this method has no effect.
        /// </para>
        /// <para>
        ///  For more information on when you need to use this method, see <see cref="MicroSDCard"/>.
        /// </para>
        /// </remarks>
        public void MountMicroSDCard()
        {
            if (!IsCardMounted)
            {
                try
                {
                    //_storage = new PersistentStorage("SD");
                    //_storage.Mount();
                    Mainboard.MountStorageDevice("SD");
                    IsCardMounted = true;
                    Thread.Sleep(500);
                }
                catch
                {
                    ErrorPrint("Error mounting SD card - no card detected.");
                }
            }
        }

        /// <summary>
        /// Attempts to dismount the file system associated with a non-volatile memory card.
        /// </summary>
        /// <remarks>
        /// <para>
        ///  Use <see cref="MountMicroSDCard"/> and <see cref="UnmountMicroSDCard"/>
        ///  to manually mount and dismount the file system on the non-volatile memory card.
        /// </para>
        /// <para>
        ///  If you call this method when there is no memory card inserted into the 
        ///  slot, or the card is already dismounted, this method has no effect.
        /// </para>
        /// <para>
        ///  For more information on when you need to use this method, see <see cref="MicroSDCard"/>.
        /// </para>
        /// </remarks>
        public void UnmountMicroSDCard()
        {
            if (IsCardMounted)
            {
                try
                {
                    //_storage.Unmount();
                    //_storage.Dispose();
                    IsCardMounted = false;
                    Mainboard.UnmountStorageDevice("SD");
                    //IsCardMounted = false;
                    Thread.Sleep(500);
                    _device = null;
                }
                catch
                {
                    _device = null;
                    ErrorPrint("Unable to unmount SD card - no card detected.");
                }
            }
        }


        private void Insert(object sender, MediaEventArgs e)
        {
            if (e.Volume.Name.Length >= 2 && e.Volume.Name.Substring(0, 2) == "SD")
            {
                if (e.Volume.FileSystem != null)
                {
                    _device = new StorageDevice(e.Volume);
                    OnMicroSDCardMountedEvent(this, _device);
                }
                else
                {
                    ErrorPrint("Unable to mount SD card. Is card formatted as FAT32?");
                    UnmountMicroSDCard();
                }
            }

        }

        private void Eject(object sender, MediaEventArgs e)
        {
            if (e.Volume.Name.Length >= 2 && e.Volume.Name.Substring(0, 2) == "SD")
            {
                OnMicroSDCardUnmountedEvent(this);
            }
        }

        /// <summary>
        /// Represents the delegate that is used for the <see cref="MicroSDCardMounted"/> event.
        /// </summary>
        /// <param name="sender">The <see cref="MicroSDCard"/> object that raised the event.</param>
        /// <param name="MicroSDCard">A storage device that can be used to access the SD non-volatile memory card.</param>
        public delegate void MicroSDCardMountedEventHandler(MicroSDCard sender, StorageDevice MicroSDCard);

        /// <summary>
        /// Raised when the file system of an SD non-volatile memory card is mounted.
        /// </summary>
        /// <remarks>
        /// <para>
        ///  Handle this event to obtain a reference to a 
        ///  <see cref="T:Microsoft.Gadgeteer.StorageDevice"/> object that can be used 
        ///  to access the file system associated with an SD non-volatile memory card.
        /// </para>
        /// <note>
        ///  This event is not necessarily raised automatically. See <see cref="MicroSDCard"/> for more information.
        /// </note>
        /// </remarks>
        public event MicroSDCardMountedEventHandler MicroSDCardMounted;

        private MicroSDCardMountedEventHandler _OnMicroSDCardMounted;


        /// <summary>
        /// Raises the <see cref="MicroSDCardMounted"/> event.
        /// </summary>
        /// <param name="sender">The <see cref="MicroSDCard"/> object that raised the event.</param>
        /// <param name="MicroSDCard">A storage device that can be used to access the SD non-volatile memory card.</param>
        protected virtual void OnMicroSDCardMountedEvent(MicroSDCard sender, StorageDevice MicroSDCard)
        {
            if (_OnMicroSDCardMounted == null) _OnMicroSDCardMounted = new MicroSDCardMountedEventHandler(OnMicroSDCardMountedEvent);
            Thread.Sleep(1000);
            if (Program.CheckAndInvoke(MicroSDCardMounted, _OnMicroSDCardMounted, sender, MicroSDCard))
            {
                MicroSDCardMounted(sender, MicroSDCard);
            }
        }

        /// <summary>
        /// Represents the delegate that is used for the <see cref="MicroSDCardMounted"/> event.
        /// </summary>
        /// <param name="sender">The <see cref="MicroSDCard"/> object that raised the event.</param>
        public delegate void MicroSDCardUnmountedEventHandler(MicroSDCard sender);

        /// <summary>
        /// Raised when the file system of an SD non-volatile memory card is dismounted.
        /// </summary>
        /// <remarks>
        /// <para>
        ///  Handle this event to inform your application that the 
        ///  <see cref="T:Microsoft.Gadgeteer.StorageDevice"/> object
        ///  that you obtained via the <see cref="MicroSDCardMounted"/> event is no longer valid.
        /// </para>
        /// <note>
        ///  This event is not necessarily raised automatically. See <see cref="MicroSDCard"/> for more information.
        /// </note>
        /// </remarks>
        public event MicroSDCardUnmountedEventHandler MicroSDCardUnmounted;

        private MicroSDCardUnmountedEventHandler _OnMicroSDCardUnmounted;


        /// <summary>
        /// Raises the <see cref="MicroSDCardUnmounted"/> event.
        /// </summary>
        /// <param name="sender">The <see cref="MicroSDCard"/> object that raised the event.</param>
        protected virtual void OnMicroSDCardUnmountedEvent(MicroSDCard sender)
        {
            if (_OnMicroSDCardUnmounted == null) _OnMicroSDCardUnmounted = new MicroSDCardUnmountedEventHandler(OnMicroSDCardUnmountedEvent);
            if (Program.CheckAndInvoke(MicroSDCardUnmounted, _OnMicroSDCardUnmounted, sender))
            {
                MicroSDCardUnmounted(sender);
            }
        }

    }
}