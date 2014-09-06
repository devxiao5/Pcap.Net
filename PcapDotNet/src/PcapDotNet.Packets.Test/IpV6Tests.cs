using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PcapDotNet.Base;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.IpV6;
using PcapDotNet.Packets.TestUtils;
using PcapDotNet.Packets.Transport;
using PcapDotNet.TestUtils;

namespace PcapDotNet.Packets.Test
{
    /// <summary>
    /// Summary description for IpV6Tests
    /// </summary>
    [TestClass]
    public class IpV6Tests
    {
        /// <summary>
        /// Gets or sets the test context which provides
        /// information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes

        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //

        #endregion

        [TestMethod]
        public void RandomIpV6Test()
        {
            MacAddress ethernetSource = new MacAddress("00:01:02:03:04:05");
            MacAddress ethernetDestination = new MacAddress("A0:A1:A2:A3:A4:A5");
            const EthernetType EthernetType = EthernetType.IpV6;

            EthernetLayer ethernetLayer = new EthernetLayer
                                              {
                                                  Source = ethernetSource,
                                                  Destination = ethernetDestination,
                                                  EtherType = EthernetType
                                              };

            Random random = new Random();

            for (int i = 0; i != 1000; ++i)
            {
                IpV6Layer ipV6Layer = random.NextIpV6Layer();

                PayloadLayer payloadLayer = random.NextPayloadLayer(random.NextInt(0, 50 * 1024));

                List<ILayer> layers = new List<ILayer> {ethernetLayer, ipV6Layer};
                if (ipV6Layer.ExtensionHeaders.LastHeader != IpV4Protocol.EncapsulatingSecurityPayload)
                    layers.Add(payloadLayer);
                Packet packet = PacketBuilder.Build(DateTime.Now, layers);

                Assert.IsTrue(packet.IsValid, string.Format("IsValid ({0}...{1})", ipV6Layer.NextHeader, ipV6Layer.ExtensionHeaders.NextHeader));

                // Ethernet
                Assert.AreEqual(packet.Length - EthernetDatagram.HeaderLengthValue, packet.Ethernet.PayloadLength, "PayloadLength");
                Assert.AreEqual(ethernetLayer, packet.Ethernet.ExtractLayer(), "Ethernet Layer");

                // IpV6
                Assert.AreEqual(ipV6Layer, packet.Ethernet.IpV6.ExtractLayer(), "IP Layer");
                Assert.IsNotNull(ipV6Layer.GetHashCode());
                Assert.AreEqual(string.Format("{0} -> {1} ({2})", ipV6Layer.Source, ipV6Layer.CurrentDestination, ipV6Layer.NextHeader), ipV6Layer.ToString());
                for (int extensionHeaderIndex = 0; extensionHeaderIndex != packet.Ethernet.IpV6.ExtensionHeaders.Headers.Count; ++extensionHeaderIndex)
                {
                    IpV6ExtensionHeader extensionHeader = packet.Ethernet.IpV6.ExtensionHeaders[extensionHeaderIndex];
                    IpV6ExtensionHeader layerExtensionheader = ipV6Layer.ExtensionHeaders[extensionHeaderIndex];
                    Assert.AreEqual(extensionHeader, layerExtensionheader);
                    // TODO: Bring it back.
//                    Assert.AreEqual(extensionHeader.GetHashCode(), layerExtensionheader.GetHashCode());
                    IpV6ExtensionHeaderOptions extensionHeaderOptions = extensionHeader as IpV6ExtensionHeaderOptions;
                    if (extensionHeaderOptions != null)
                    {
                        foreach (IpV6Option option in extensionHeaderOptions.Options)
                        {
                            switch (option.OptionType)
                            {
                                case IpV6OptionType.SmfDpd:
                                    IpV6OptionSmfDpd optionSmfDpd = (IpV6OptionSmfDpd)option;
                                    Assert.AreEqual(optionSmfDpd is IpV6OptionSmfDpdSequenceHashAssistValue, optionSmfDpd.HashIndicator);
                                    break;

                                case IpV6OptionType.QuickStart:
                                    IpV6OptionQuickStart optionQuickStart = (IpV6OptionQuickStart)option;
                                    MoreAssert.IsBiggerOrEqual(0, optionQuickStart.RateKbps);
                                    break;
                            }
                        }
                    }
                    IpV6ExtensionHeaderMobility extensionHeaderMobility = extensionHeader as IpV6ExtensionHeaderMobility;
                    if (extensionHeaderMobility != null)
                    {
                        Assert.IsFalse(extensionHeaderMobility.Equals(2));
                        Assert.IsTrue(extensionHeaderMobility.Equals((object)extensionHeader));
                        Assert.AreEqual(extensionHeaderMobility.MobilityOptions, new IpV6MobilityOptions(extensionHeaderMobility.MobilityOptions).AsEnumerable());
                        foreach (IpV6MobilityOption option in extensionHeaderMobility.MobilityOptions)
                        {
                            switch (option.OptionType)
                            {
                                case IpV6MobilityOptionType.BindingIdentifier:
                                    IpV6MobilityOptionBindingIdentifier optionBindingIdentifier = (IpV6MobilityOptionBindingIdentifier)option;
                                    if (optionBindingIdentifier.IpV4CareOfAddress.HasValue)
                                        Assert.AreEqual(optionBindingIdentifier.IpV4CareOfAddress.Value, optionBindingIdentifier.CareOfAddress);
                                    else if (optionBindingIdentifier.IpV6CareOfAddress.HasValue)
                                        Assert.AreEqual(optionBindingIdentifier.IpV6CareOfAddress.Value, optionBindingIdentifier.CareOfAddress);
                                    else
                                        Assert.IsNull(optionBindingIdentifier.CareOfAddress);
                                    break;

                                case IpV6MobilityOptionType.AccessNetworkIdentifier:
                                    IpV6MobilityOptionAccessNetworkIdentifier optionAccessNetworkIdentifier = (IpV6MobilityOptionAccessNetworkIdentifier)option;
                                    foreach (IpV6AccessNetworkIdentifierSubOption subOption in optionAccessNetworkIdentifier.SubOptions)
                                    {
                                        switch (subOption.OptionType)
                                        {
                                            case IpV6AccessNetworkIdentifierSubOptionType.GeoLocation:
                                                IpV6AccessNetworkIdentifierSubOptionGeoLocation subOptionGeoLocation =
                                                    (IpV6AccessNetworkIdentifierSubOptionGeoLocation)subOption;
                                                MoreAssert.IsBiggerOrEqual(-90, subOptionGeoLocation.LatitudeDegreesReal);
                                                MoreAssert.IsSmallerOrEqual(90, subOptionGeoLocation.LatitudeDegreesReal);
                                                MoreAssert.IsBiggerOrEqual(-180, subOptionGeoLocation.LongitudeDegreesReal);
                                                MoreAssert.IsSmallerOrEqual(180, subOptionGeoLocation.LongitudeDegreesReal);
                                                IpV6AccessNetworkIdentifierSubOptionGeoLocation subOptionGetLocationFromReal =
                                                    IpV6AccessNetworkIdentifierSubOptionGeoLocation.CreateFromRealValues(
                                                        subOptionGeoLocation.LatitudeDegreesReal,
                                                        subOptionGeoLocation.LongitudeDegreesReal);
                                                Assert.AreEqual(subOptionGeoLocation, subOptionGetLocationFromReal);
                                                break;
                                        }
                                    }
                                    break;

                                case IpV6MobilityOptionType.Timestamp:
                                    IpV6MobilityOptionTimestamp optionTimestamp = (IpV6MobilityOptionTimestamp)option;
                                    MoreAssert.IsBiggerOrEqual(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), optionTimestamp.TimestampDateTime);
                                    break;

                                case IpV6MobilityOptionType.FlowIdentification:
                                    IpV6MobilityOptionFlowIdentification optionFlowIdentification = (IpV6MobilityOptionFlowIdentification)option;
                                    foreach (IpV6FlowIdentificationSubOption subOption in optionFlowIdentification.SubOptions)
                                    {
                                        switch (subOption.OptionType)
                                        {
                                            case IpV6FlowIdentificationSubOptionType.BindingReference:
                                                IpV6FlowIdentificationSubOptionBindingReference subOptionBindingReference =
                                                    (IpV6FlowIdentificationSubOptionBindingReference)subOption;
                                                Assert.AreEqual(subOptionBindingReference,
                                                                new IpV6FlowIdentificationSubOptionBindingReference(
                                                                    subOptionBindingReference.BindingIds.AsEnumerable()));
                                                break;
                                        }
                                    }
                                    break;

                                case IpV6MobilityOptionType.CgaParameters:
                                    IpV6MobilityOptionCgaParameters optionCgaParameters = (IpV6MobilityOptionCgaParameters)option;
                                    Assert.AreEqual(optionCgaParameters.Length - 2, optionCgaParameters.CgaParameters.Length);
                                    break;

                                case IpV6MobilityOptionType.CareOfTest:
                                    IpV6MobilityOptionCareOfTest optionCareOfTest = (IpV6MobilityOptionCareOfTest)option;
                                    Assert.IsInstanceOfType(optionCareOfTest.CareOfKeygenToken, typeof(ulong));
                                    break;

                                case IpV6MobilityOptionType.IpV4CareOfAddress:
                                    IpV6MobilityOptionIpV4CareOfAddress optionIpV4CareOfAddress = (IpV6MobilityOptionIpV4CareOfAddress)option;
                                    Assert.IsNotNull(optionIpV4CareOfAddress.CareOfAddress);
                                    break;

                                case IpV6MobilityOptionType.ReplayProtection:
                                    IpV6MobilityOptionReplayProtection optionReplayProtection = (IpV6MobilityOptionReplayProtection)option;
                                    Assert.IsNotNull(optionReplayProtection.Timestamp);
                                    break;

                                case IpV6MobilityOptionType.Experimental:
                                    IpV6MobilityOptionExperimental optionExperimental = (IpV6MobilityOptionExperimental)option;
                                    Assert.IsNotNull(optionExperimental.Data);
                                    break;

                                case IpV6MobilityOptionType.PermanentHomeKeygenToken:
                                    IpV6MobilityOptionPermanentHomeKeygenToken optionPermanentHomeKeygenToken =
                                        (IpV6MobilityOptionPermanentHomeKeygenToken)option;
                                    Assert.IsNotNull(optionPermanentHomeKeygenToken.PermanentHomeKeygenToken);
                                    break;

                                case IpV6MobilityOptionType.Signature:
                                    IpV6MobilityOptionSignature optionSignature = (IpV6MobilityOptionSignature)option;
                                    Assert.IsNotNull(optionSignature.Signature);
                                    break;
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void AutomaticIpV6NextHeader()
        {
            Packet packet = PacketBuilder.Build(DateTime.Now, new EthernetLayer(), new IpV6Layer(), new UdpLayer());
            Assert.AreEqual(IpV4Protocol.Udp, packet.Ethernet.IpV6.NextHeader);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
        public void AutomaticIpV6NextHeaderNoNextLayer()
        {
            PacketBuilder.Build(DateTime.Now, new EthernetLayer(), new IpV6Layer());
            Assert.Fail();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
        public void AutomaticIpV6NextHeaderUnknownNextLayer()
        {
            PacketBuilder.Build(DateTime.Now, new EthernetLayer(), new IpV6Layer(), new PayloadLayer());
            Assert.Fail();
        }

        [TestMethod]
        public void IpV6OptionCalipsoChecksum()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Udp,
                                new IpV6Options(new IpV6OptionCalipso(IpV6CalipsoDomainOfInterpretation.Null, 0, null, DataSegment.Empty)))),
                    },
                new UdpLayer());
            Assert.IsTrue(packet.IsValid);
            Assert.IsTrue(((IpV6OptionCalipso)((IpV6ExtensionHeaderDestinationOptions)packet.Ethernet.IpV6.ExtensionHeaders[0]).Options[0]).IsChecksumCorrect);
        }

        [TestMethod]
        public void IpV6OptionUnknown()
        {
            IpV6OptionUnknown option = new IpV6OptionUnknown((IpV6OptionType)0xBB, DataSegment.Empty);
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(IpV4Protocol.Skip, new IpV6Options(option)))
                    });
            Assert.IsTrue(packet.IsValid);
            Assert.AreEqual(option, ((IpV6ExtensionHeaderDestinationOptions)packet.Ethernet.IpV6.ExtensionHeaders[0]).Options[0]);
        }

        [TestMethod]
        public void IpV6MobilityOptionUnknown()
        {
            Random random = new Random();
            DataSegment data = random.NextDataSegment(random.NextInt(0, 100));
            IpV6MobilityOptionUnknown option = new IpV6MobilityOptionUnknown((IpV6MobilityOptionType)0xBB, data);
            Assert.AreEqual(data, option.Data);
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                                                        new IpV6MobilityOptions(option)))
                    });
            Assert.IsTrue(packet.IsValid);
            Assert.AreEqual(option, ((IpV6ExtensionHeaderMobility)packet.Ethernet.IpV6.ExtensionHeaders[0]).MobilityOptions[0]);
        }

        [TestMethod]
        public void IpV6AccessNetworkIdentifierSubOptionUnknown()
        {
            IpV6AccessNetworkIdentifierSubOptionUnknown subOption =
                new IpV6AccessNetworkIdentifierSubOptionUnknown((IpV6AccessNetworkIdentifierSubOptionType)100, DataSegment.Empty);
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionAccessNetworkIdentifier(
                                        new IpV6AccessNetworkIdentifierSubOptions(subOption)))))
                    });
            Assert.IsTrue(packet.IsValid);
            Assert.AreEqual(subOption,
                            ((IpV6MobilityOptionAccessNetworkIdentifier)
                             ((IpV6ExtensionHeaderMobility)packet.Ethernet.IpV6.ExtensionHeaders[0]).MobilityOptions[0]).SubOptions[0]);
        }

        [TestMethod]
        public void IpV6FlowIdentificationSubOptionUnknown()
        {
            IpV6FlowIdentificationSubOptionUnknown subOption =
                new IpV6FlowIdentificationSubOptionUnknown((IpV6FlowIdentificationSubOptionType)100, DataSegment.Empty);
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionFlowIdentification(0, 0, IpV6FlowIdentificationStatus.FlowIdentifierNotFound,
                                                                             new IpV6FlowIdentificationSubOptions(subOption)))))
                    });
            Assert.IsTrue(packet.IsValid);
            Assert.AreEqual(subOption,
                            ((IpV6MobilityOptionFlowIdentification)
                             ((IpV6ExtensionHeaderMobility)packet.Ethernet.IpV6.ExtensionHeaders[0]).MobilityOptions[0]).SubOptions[0]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6ExtensionHeaderRoutingRplCommonPrefixLengthForNonLastAddressesTooBig()
        {
            Assert.IsNull(new IpV6ExtensionHeaderRoutingRpl(IpV4Protocol.Skip, 0, 16, 0, new IpV6Address[0]));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6ExtensionHeaderRoutingRplCommonPrefixLengthForLastAddressTooBig()
        {
            Assert.IsNull(new IpV6ExtensionHeaderRoutingRpl(IpV4Protocol.Skip, 0, 0, 16, new IpV6Address[0]));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
        public void IpV6ExtensionHeadersEncapsulatingSecurityPayloadBeforeLast()
        {
            Assert.IsNull(new IpV6ExtensionHeaders(new IpV6ExtensionHeaderEncapsulatingSecurityPayload(0, 0, DataSegment.Empty),
                                                   new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0, false, 0)));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
        public void IpV6ExtensionHeaderAuthenticationNonIntegralMultipleOf4Bytes()
        {
            Assert.IsNull(new IpV6ExtensionHeaderAuthentication(IpV4Protocol.Skip, 0, 0, new DataSegment(new byte[6])));
        }

        [TestMethod]
        public void IpV6ExtensionHeadersConstructors()
        {
            IEnumerable<IpV6ExtensionHeader> extensionHeadersEnumerable = new IpV6ExtensionHeader[0];
            IList<IpV6ExtensionHeader> extensionHeadersIList = new IpV6ExtensionHeader[0];
            Assert.AreEqual(new IpV6ExtensionHeaders(extensionHeadersEnumerable), new IpV6ExtensionHeaders(extensionHeadersIList));
        }

        [TestMethod]
        public void IpV6MobilityOptionFlowSummaryConstructors()
        {
            IEnumerable<ushort> flowIdentifiersEnumerable = new ushort[1];
            IList<ushort> flowIdentifiersIList = new ushort[1];
            Assert.AreEqual(new IpV6MobilityOptionFlowSummary(flowIdentifiersEnumerable), new IpV6MobilityOptionFlowSummary(flowIdentifiersIList));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionFlowSummaryNoIdentifiers()
        {
            Assert.IsNull(new IpV6MobilityOptionFlowSummary(new ushort[0]));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationLatitudeIntegerTooBig()
        {
            Assert.IsNull(new IpV6AccessNetworkIdentifierSubOptionGeoLocation((UInt24)0x7FFFFF, 0));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationLatitudeIntegerTooSmall()
        {
            Assert.IsNull(new IpV6AccessNetworkIdentifierSubOptionGeoLocation((UInt24)0x800000, 0));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationLongtitudeIntegerTooBig()
        {
            Assert.IsNull(new IpV6AccessNetworkIdentifierSubOptionGeoLocation(0, (UInt24)0x7FFFFF));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationLongtitudeIntegerTooSmall()
        {
            Assert.IsNull(new IpV6AccessNetworkIdentifierSubOptionGeoLocation(0, (UInt24)0x800000));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationCreateFromRealValuesLatitudeTooBig()
        {
            Assert.IsNull(IpV6AccessNetworkIdentifierSubOptionGeoLocation.CreateFromRealValues(90.1, 0));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationCreateFromRealValuesLatitudeTooSmall()
        {
            Assert.IsNull(IpV6AccessNetworkIdentifierSubOptionGeoLocation.CreateFromRealValues(-90.1, 0));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationCreateFromRealValuesLongtitudeTooBig()
        {
            Assert.IsNull(IpV6AccessNetworkIdentifierSubOptionGeoLocation.CreateFromRealValues(0, 180.1));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationCreateFromRealValuesLongtitudeTooSmall()
        {
            Assert.IsNull(IpV6AccessNetworkIdentifierSubOptionGeoLocation.CreateFromRealValues(0, -180.1));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionNetworkIdentifierNetworkNameTooLong()
        {
            Assert.IsNull(new IpV6AccessNetworkIdentifierSubOptionNetworkIdentifier(false, new DataSegment(new byte[256]), DataSegment.Empty));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6AccessNetworkIdentifierSubOptionNetworkIdentifierAccessPointNameTooLong()
        {
            Assert.IsNull(new IpV6AccessNetworkIdentifierSubOptionNetworkIdentifier(false, DataSegment.Empty, new DataSegment(new byte[256])));
        }

        [TestMethod]
        public void IpV6AccessNetworkIdentifierSubOptionNetworkIdentifierDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionAccessNetworkIdentifier(
                                        new IpV6AccessNetworkIdentifierSubOptions(
                                            new IpV6AccessNetworkIdentifierSubOptionNetworkIdentifier(false, DataSegment.Empty, DataSegment.Empty))))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6AccessNetworkIdentifierSubOptionNetworkIdentifierDataTooShortForNetworkName()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionAccessNetworkIdentifier(
                                        new IpV6AccessNetworkIdentifierSubOptions(
                                            new IpV6AccessNetworkIdentifierSubOptionNetworkIdentifier(false, new DataSegment(new byte[1]), DataSegment.Empty))))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6AccessNetworkIdentifierSubOptionNetworkIdentifierDataTooShortForAccessPointName()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionAccessNetworkIdentifier(
                                        new IpV6AccessNetworkIdentifierSubOptions(
                                            new IpV6AccessNetworkIdentifierSubOptionNetworkIdentifier(false, DataSegment.Empty, new DataSegment(new byte[1])))))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6ExtensionHeaderRoutingRplCommonPrefixNotCommon()
        {
            Assert.IsNull(new IpV6ExtensionHeaderRoutingRpl(IpV4Protocol.Skip, 5, 4, 4,
                                                            new IpV6Address("0000:0000:9ABC:DEF0:1234:5678:9ABC:DEF0"),
                                                            new IpV6Address("0000:0001:9ABC:DEF0:1234:5678:9ABC:DEF0")));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
        public void IpV6OptionCalipsoCompartmentBitmapDoesntDivideBy4()
        {
            Assert.IsNull(new IpV6OptionCalipso(IpV6CalipsoDomainOfInterpretation.Null, 0, null, new DataSegment(new byte[6])));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6OptionCalipsoCompartmentBitmapTooLong()
        {
            Assert.IsNull(new IpV6OptionCalipso(IpV6CalipsoDomainOfInterpretation.Null, 0, null, new DataSegment(new byte[248])));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6OptionSmfDpdDefaultTaggerIdTooLong()
        {
            Assert.IsNull(new IpV6OptionSmfDpdDefault(new DataSegment(new byte[17]), DataSegment.Empty));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6OptionSmfDpdDefaultTaggerIdTooShort()
        {
            Assert.IsNull(new IpV6OptionSmfDpdDefault(DataSegment.Empty, DataSegment.Empty));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionMobileNodeIdentifierIdentifierTooShort()
        {
            Assert.IsNull(new IpV6MobilityOptionMobileNodeIdentifier(IpV6MobileNodeIdentifierSubtype.NetworkAccessIdentifier, DataSegment.Empty));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionContextRequestEntryOptionLengthTooBig()
        {
            Assert.IsNull(new IpV6MobilityOptionContextRequestEntry(0, new DataSegment(new byte[256])));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionBindingIdentifierPriorityTooBig()
        {
            Assert.IsNull(new IpV6MobilityOptionBindingIdentifier(0, IpV6BindingAcknowledgementStatus.AcceptedBut, false, 0x80));
        }

        [TestMethod]
        public void IpV6ExtensionHeaderTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(IpV4Protocol.Skip, IpV6Options.Empty))
                    });
            Assert.IsTrue(packet.IsValid);
            Packet invalidPacket = new Packet(packet.Buffer.Take(packet.Length - 1).ToArray(), DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6ExtensionHeaderAuthenticationTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderAuthentication(IpV4Protocol.Skip, 0, 0, DataSegment.Empty))
                    });
            Assert.IsTrue(packet.IsValid);
            Packet invalidPacket = new Packet(packet.Buffer.Take(packet.Length - 1).ToArray(), DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6ExtensionHeaderAuthenticationBadPayloadLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderAuthentication(IpV4Protocol.Skip, 0, 0, new DataSegment(new byte[12])))
                    });
            Assert.IsTrue(packet.IsValid);
            ++packet.Buffer[14 + 40 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6StandardExtensionHeaderTooShort()
        {
            byte[] payload = new byte[8];
            payload[1] = 10;
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        NextHeader = IpV4Protocol.IpV6Route
                    },
                new PayloadLayer {Data = new Datagram(payload)});
            Assert.IsFalse(packet.IsValid);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
        public void IpV6LayerWithLayerAfterEncapsulatingSecurityPayload()
        {
            Assert.IsNull(
                PacketBuilder.Build(
                    DateTime.Now,
                    new EthernetLayer(),
                    new IpV6Layer
                        {
                            ExtensionHeaders = new IpV6ExtensionHeaders(new IpV6ExtensionHeaderEncapsulatingSecurityPayload(0, 0, DataSegment.Empty))
                        },
                    new PayloadLayer {Data = new Datagram(new byte[10])}));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionContextRequestTooLong()
        {
            Assert.IsNull(new IpV6MobilityOptionContextRequest(
                              new IpV6MobilityOptionContextRequestEntry(0, new DataSegment(new byte[100])),
                              new IpV6MobilityOptionContextRequestEntry(0, new DataSegment(new byte[100])),
                              new IpV6MobilityOptionContextRequestEntry(0, new DataSegment(new byte[100]))));
        }

        [TestMethod]
        public void IpV6MobilityOptionContextRequestEntryEquals()
        {
            Assert.AreNotEqual(new IpV6MobilityOptionContextRequestEntry(0, DataSegment.Empty), 1);
            Assert.AreEqual(new IpV6MobilityOptionContextRequestEntry(0, DataSegment.Empty), new IpV6MobilityOptionContextRequestEntry(0, DataSegment.Empty));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionFlowIdentificationSubOptionsTooLong()
        {
            Assert.IsNull(new IpV6MobilityOptionFlowIdentification(0, 0, IpV6FlowIdentificationStatus.FlowBindingSuccessful,
                                                                   new IpV6FlowIdentificationSubOptions(new IpV6FlowIdentificationSubOptionPadN(500))));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionServiceSelectionConstructorDataTooShort()
        {
            Assert.IsNull(new IpV6MobilityOptionServiceSelection(DataSegment.Empty));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionServiceSelectionDataTooLong()
        {
            Assert.IsNull(new IpV6MobilityOptionServiceSelection(new DataSegment(new byte[256])));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6OptionLineIdentificationDestinationLineIdentificationTooLong()
        {
            Assert.IsNull(new IpV6OptionLineIdentificationDestination(new DataSegment(new byte[256])));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionIpV4AddressAcknowledgementPrefixLengthTooBig()
        {
            Assert.IsNull(new IpV6MobilityOptionIpV4AddressAcknowledgement(IpV6AddressAcknowledgementStatus.Success, 0x40, IpV4Address.Zero));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionIpV4HomeAddressPrefixLengthTooBig()
        {
            Assert.IsNull(new IpV6MobilityOptionIpV4HomeAddress(0x40, false, IpV4Address.Zero));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionIpV4HomeAddressReplyPrefixLengthTooBig()
        {
            Assert.IsNull(new IpV6MobilityOptionIpV4HomeAddressReply(IpV6IpV4HomeAddressReplyStatus.Success, 0x40, IpV4Address.Zero));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionIpV4HomeAddressRequestPrefixLengthTooBig()
        {
            Assert.IsNull(new IpV6MobilityOptionIpV4HomeAddressRequest(0x40, IpV4Address.Zero));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionIpV6AddressPrefixPrefixLengthTooBig()
        {
            Assert.IsNull(new IpV6MobilityOptionIpV6AddressPrefix(IpV6MobilityIpV6AddressPrefixCode.NewCareOfAddress, 129, IpV6Address.Zero));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionAccessNetworkIdentifierSubOptionsTooLong()
        {
            Assert.IsNull(
                new IpV6MobilityOptionAccessNetworkIdentifier(
                    new IpV6AccessNetworkIdentifierSubOptions(
                        new IpV6AccessNetworkIdentifierSubOptionOperatorIdentifier(IpV6AccessNetworkIdentifierOperatorIdentifierType.PrivateEnterpriseNumber,
                                                                                   new DataSegment(new byte[254])))));
        }

        [TestMethod]
        public void IpV6MobilityOptionRedirectCreateInstanceDataShorterThanMinimum()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionRedirect(IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 24 + 1] = 1;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionRedirectCreateInstanceDataTooShortForIpV4()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionRedirect(IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 24 + 1] = 3;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionRedirectCreateInstanceDataTooShortForIpV6()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionRedirect(IpV6Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 24 + 1] = 15;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionRedirectCreateInstanceNotIpV6OrIpV4()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionRedirect(IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 24 + 2] = 0;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6ExtensionHeaderRoutingRplParseRoutingDataPadSizeMakeNumAddressesNegative()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderRoutingRpl(IpV4Protocol.Skip, 8, 8, 0))
                    });
            packet.Buffer[14 + 40 + 5] = 0x10;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6ExtensionHeaderRoutingParseDataNimrod()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderRoutingHomeAddress(IpV4Protocol.Skip, 0, IpV6Address.Zero))
                    });
            packet.Buffer[14 + 40 + 2] = (byte)IpV6RoutingType.Nimrod;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6ExtensionHeaderRoutingParseDataUnknownRoutingType()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderRoutingHomeAddress(IpV4Protocol.Skip, 0, IpV6Address.Zero))
                    });
            packet.Buffer[14 + 40 + 2] = 0x55;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6ExtensionHeaderFragmentDataFragmentOffsetTooBig()
        {
            Assert.IsNull(new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0x2000, false, 0));
        }

        [TestMethod]
        public void IpV6ExtensionHeaderFragmentDataEquals()
        {
            Assert.AreNotEqual(new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0, false, 0),
                               new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Sm, 0, false, 0));
            Assert.AreNotEqual(new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0, false, 0),
                               new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 1, false, 0));
            Assert.AreNotEqual(new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0, false, 0),
                               new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0, true, 0));
            Assert.AreNotEqual(new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0, false, 0),
                               new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0, false, 1));
        }

        [TestMethod]
        public void IpV6ExtensionHeaderFragmentDataWrongLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders = new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderFragmentData(IpV4Protocol.Skip, 0, false, 0))
                    },
                new PayloadLayer {Data = new Datagram(new byte[100])});
            Assert.IsTrue(packet.IsValid);
            ++packet.Buffer[14 + 40 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionLocalMobilityAnchorAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionLocalMobilityAnchorAddress(IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 24 + 1] -= 5;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionLocalMobilityAnchorAddressDataLengthDoesntMatchIpV6()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionLocalMobilityAnchorAddress(IpV6Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            ++packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionLocalMobilityAnchorAddressDataLengthDoesntMatchIpV4()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionLocalMobilityAnchorAddress(IpV4Address.Zero),
                                    new IpV6MobilityOptionPadN(10))))
                    });
            Assert.IsTrue(packet.IsValid);
            ++packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionLocalMobilityAnchorAddressBadCode()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionLocalMobilityAnchorAddress(IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 24 + 2] = 5;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileNodeIdentifierCreateInstanceDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionMobileNodeIdentifier((IpV6MobileNodeIdentifierSubtype)2, DataSegment.Empty))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileNodeIdentifierCreateInstanceNetworkAccessIdentifierTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionMobileNodeIdentifier(IpV6MobileNodeIdentifierSubtype.NetworkAccessIdentifier,
                                                                                                   new DataSegment(new byte[1])))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileNodeIdentifierEquals()
        {
            Assert.AreEqual(new IpV6MobilityOptionMobileNodeIdentifier(IpV6MobileNodeIdentifierSubtype.NetworkAccessIdentifier, new DataSegment(new byte[1])),
                            new IpV6MobilityOptionMobileNodeIdentifier(IpV6MobileNodeIdentifierSubtype.NetworkAccessIdentifier, new DataSegment(new byte[1])));
            Assert.AreNotEqual(
                new IpV6MobilityOptionMobileNodeIdentifier(IpV6MobileNodeIdentifierSubtype.NetworkAccessIdentifier, new DataSegment(new byte[1])),
                new IpV6MobilityOptionMobileNodeIdentifier((IpV6MobileNodeIdentifierSubtype)2, new DataSegment(new byte[1])));
            Assert.AreNotEqual(
                new IpV6MobilityOptionMobileNodeIdentifier(IpV6MobileNodeIdentifierSubtype.NetworkAccessIdentifier, new DataSegment(new byte[1])),
                new IpV6MobilityOptionMobileNodeIdentifier(IpV6MobileNodeIdentifierSubtype.NetworkAccessIdentifier, new DataSegment(new byte[2])));
            Assert.IsFalse(
                new IpV6MobilityOptionMobileNodeIdentifier(IpV6MobileNodeIdentifierSubtype.NetworkAccessIdentifier, new DataSegment(new byte[1])).Equals(null));
        }

        [TestMethod]
        public void IpV6OptionsDataTooShortForReadingOptionDataLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionPadN(3), new IpV6OptionPad1())))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 7] = (byte)IpV6OptionType.PadN;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionsDataTooShortForOptionDataLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionPadN(4))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 3] = 50;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionsEnumerableConstructor()
        {
            IpV6Options options = new IpV6Options(new IpV6Option[] {new IpV6OptionPad1()}.Concat(new IpV6OptionPad1()));
            Assert.AreEqual(2, options.Count);
            Assert.AreEqual(new IpV6OptionPad1(), options[0]);
            Assert.AreEqual(new IpV6OptionPad1(), options[1]);
        }

        [TestMethod]
        public void IpV6OptionSmfDpdSequenceBasedDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionSmfDpdIpV4(IpV4Address.Zero, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionSmfDpdSequenceBasedIpV4TaggerIdWrongLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionSmfDpdIpV4(IpV4Address.Zero, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 2 + 2] &= 0xF0;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionSmfDpdSequenceBasedIpV6TaggerIdWrongLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionSmfDpdIpV6(IpV6Address.Zero, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 2 + 2] &= 0xF0;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionSmfDpdSequenceBasedUnknownTaggerIdType()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionSmfDpdIpV4(IpV4Address.Zero, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 2 + 2] |= 0x70;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionSmfDpdSequenceBasedEqualsData()
        {
            Assert.AreEqual(new IpV6OptionSmfDpdIpV6(IpV6Address.Zero, DataSegment.Empty),
                            new IpV6OptionSmfDpdIpV6(IpV6Address.Zero, DataSegment.Empty));
            Assert.AreNotEqual(new IpV6OptionSmfDpdIpV6(IpV6Address.Zero, DataSegment.Empty),
                               new IpV6OptionSmfDpdIpV4(IpV4Address.Zero, new DataSegment(new byte[12])));
            Assert.AreNotEqual(new IpV6OptionSmfDpdIpV6(IpV6Address.Zero, DataSegment.Empty),
                               new IpV6OptionSmfDpdSequenceHashAssistValue(new DataSegment(new byte[17])));
            Assert.AreNotEqual(new IpV6OptionSmfDpdDefault(new DataSegment(new byte[16]), DataSegment.Empty),
                               new IpV6OptionSmfDpdIpV6(IpV6Address.Zero, DataSegment.Empty));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), AllowDerivedTypes = false)]
        public void IpV6MobilityOptionCgaParametersTooLong()
        {
            Assert.IsNull(new IpV6MobilityOptionCgaParameters(new DataSegment(new byte[256])));
            Assert.Fail();
        }

        [TestMethod]
        public void IpV6MobilityOptionCareOfTestCreateInstanceDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionCareOfTest(0))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionIpV4CareOfAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionIpV4CareOfAddress(IpV4Address.Zero))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionReplayProtectionDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionReplayProtection(0))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionCareOfTestInitDataTooLong()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionCareOfTestInit())))

                    });
            Assert.IsTrue(packet.IsValid);
            ++packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionAlternateCareOfAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionAlternateCareOfAddress(IpV6Address.Zero))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileAccessGatewayIpV6AddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionMobileAccessGatewayIpV6Address(IpV6Address.Zero))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileAccessGatewayIpV6AddressWrongAddressLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionMobileAccessGatewayIpV6Address(IpV6Address.Zero))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 3];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionHomeNetworkPrefixDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionHomeNetworkPrefix(0, IpV6Address.Zero))))

                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionLineIdentificationDestinationDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionLineIdentificationDestination(DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionLineIdentificationDestinationWrongLineIdentificationLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionLineIdentificationDestination(DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            ++packet.Buffer[14 + 40 + 2 + 2];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionEndpointIdentificationDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionEndpointIdentification(DataSegment.Empty, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionEndpointIdentificationWrongSourceLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionEndpointIdentification(DataSegment.Empty, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            ++packet.Buffer[14 + 40 + 2 + 2];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionFlowSummaryDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionFlowSummary(new ushort[1]))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionBindingIdentifierDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionBindingIdentifier(0, IpV6BindingAcknowledgementStatus.InsufficientResources, false,
                                                                                                0))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionBindingIdentifierDataBadLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionBindingIdentifier(0, IpV6BindingAcknowledgementStatus.InsufficientResources, false,
                                                                                                0, IpV6Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionContextRequestDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionContextRequest(new IpV6MobilityOptionContextRequestEntry(0, DataSegment.Empty)))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionContextRequestDataTooShortForRequestLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionContextRequest(new IpV6MobilityOptionContextRequestEntry(0, new DataSegment(new byte[10]))))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionCalipsoDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionCalipso(IpV6CalipsoDomainOfInterpretation.Null, 0, null, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionCalipsoDataTooShortForCompartmentBitmap()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderDestinationOptions(
                            IpV4Protocol.Skip, new IpV6Options(new IpV6OptionCalipso(IpV6CalipsoDomainOfInterpretation.Null, 0, null, new DataSegment(new byte[8])))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionAlternateIpV4CareOfAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionAlternateIpV4CareOfAddress(IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionAuthenticationDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionAuthentication(IpV6AuthenticationSubtype.HomeAgent, 0, DataSegment.Empty))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionBindingAuthorizationDataForFmIpV6DataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionBindingAuthorizationDataForFmIpV6(0, DataSegment.Empty))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionBindingRefreshAdviceDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionBindingRefreshAdvice(0))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionDnsUpdateDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionDnsUpdate(IpV6DnsUpdateStatus.ReasonUnspecified, false, DataSegment.Empty))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionFlowIdentificationDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionFlowIdentification(0, 0, IpV6FlowIdentificationStatus.FlowBindingSuccessful, IpV6FlowIdentificationSubOptions.None))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionGreKeyDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionGreKey(0))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionIpV4AddressAcknowledgementDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionIpV4AddressAcknowledgement(IpV6AddressAcknowledgementStatus.Success, 0, IpV4Address.Zero))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionIpV4DhcpSupportModeDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionIpV4DhcpSupportMode(false))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionIpV4HomeAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionIpV4HomeAddress(0, false, IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionIpV4HomeAddressReplyDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionIpV4HomeAddressReply(IpV6IpV4HomeAddressReplyStatus.Success, 0, IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionIpV4HomeAddressRequestDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionIpV4HomeAddressRequest(0, IpV4Address.Zero))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionIpV6AddressPrefixDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionIpV6AddressPrefix(IpV6MobilityIpV6AddressPrefixCode.NewCareOfAddress, 0,
                                                                                                IpV6Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionLinkLayerAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionLinkLayerAddress(IpV6MobilityLinkLayerAddressCode.MobilityNode, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionLoadInformationDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionLoadInformation(0, 0, 0, 0, 0))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileNodeGroupIdentifierDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionMobileNodeGroupIdentifier(IpV6MobileNodeGroupIdentifierSubType.BulkBindingUpdateGroup, 0))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileNodeLinkLayerIdentifierDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionMobileNodeLinkLayerIdentifier(DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileNodeLinkLocalAddressInterfaceIdentifierDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionMobileNodeLinkLocalAddressInterfaceIdentifier(0))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionNatDetectionDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionNatDetection(false, 0))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionNonceIndicesDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionNonceIndices(0, 0))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionAccessTechnologyTypeDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionAccessTechnologyType(IpV6AccessTechnologyType.Ethernet))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionRestartCounterDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionRestartCounter(0))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionServiceSelectionDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionServiceSelection(new DataSegment(new byte[1])))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionTransientBindingDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionTransientBinding(false, 0))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionVendorSpecificDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionVendorSpecific(0, 0, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionCgaParametersRequestDataTooLong()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionCgaParametersRequest())))
                });
            Assert.IsTrue(packet.IsValid);
            ++packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionHandoffIndicatorDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionHandoffIndicator(IpV6HandoffIndicator.HandoffBetweenTwoDifferentInterfacesOfTheMobileNode))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionIpV4DefaultRouterAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionIpV4DefaultRouterAddress(IpV4Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionLinkLocalAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionLinkLocalAddress(IpV6Address.Zero))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionMobileNetworkPrefixDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionMobileNetworkPrefix(0, IpV6Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionRedirectCapabilityDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(new IpV6MobilityOptionRedirectCapability())))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6MobilityOptionTimestampDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(new IpV6MobilityOptionTimestamp(0))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionHomeAddressDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionHomeAddress(IpV6Address.Zero))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionJumboPayloadDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderDestinationOptions(
                            IpV4Protocol.Skip, new IpV6Options(new IpV6OptionJumboPayload(0))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionQuickStartDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderDestinationOptions(
                            IpV4Protocol.Skip, new IpV6Options(new IpV6OptionQuickStart(IpV4OptionQuickStartFunction.RateRequest, 0, 0, 0))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionRouterAlertDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderDestinationOptions(
                            IpV4Protocol.Skip, new IpV6Options(new IpV6OptionRouterAlert(IpV6RouterAlertType.Rsvp))))
                });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionRoutingProtocolLowPowerAndLossyNetworksDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip,
                                new IpV6Options(new IpV6OptionRoutingProtocolLowPowerAndLossyNetworks(false, false, false, 0, 0, DataSegment.Empty))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionTunnelEncapsulationLimitDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionTunnelEncapsulationLimit(0))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6OptionSmfDpdSequenceHashAssistValueDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderDestinationOptions(
                                IpV4Protocol.Skip, new IpV6Options(new IpV6OptionSmfDpdSequenceHashAssistValue(new DataSegment(new byte[1])))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
        public void IpV6ExtensionHeaderMobilityExperimentalConstructorMessageDataBadLength()
        {
            Assert.IsNull(new IpV6ExtensionHeaderMobilityExperimental(IpV4Protocol.Pin, 0, new DataSegment(new byte[5])));
            Assert.Fail();
        }

        [TestMethod]
        public void IpV6DatagramParseExtensionHeaderWithShorterThanHeaderLength()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    NextHeader = IpV4Protocol.Il
                });
            Assert.IsTrue(packet.IsValid);
            Packet invalidPacket = new Packet(packet.Buffer.Take(packet.Length - 1).ToArray(), DateTime.Now, DataLinkKind.Ethernet);
            Assert.AreEqual(IpV6ExtensionHeaders.Empty, invalidPacket.Ethernet.IpV6.ExtensionHeaders);
            Assert.AreEqual(0, invalidPacket.Ethernet.IpV6.RealPayloadLength);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6FlowIdentificationSubOptionTrafficSelectorDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionFlowIdentification(
                                        0, 0, IpV6FlowIdentificationStatus.FlowBindingSuccessful,
                                        new IpV6FlowIdentificationSubOptions(
                                            new IpV6FlowIdentificationSubOptionTrafficSelector(IpV6FlowIdentificationTrafficSelectorFormat.IpV4Binary,
                                                                                               DataSegment.Empty))))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 8 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6AccessNetworkIdentifierSubOptionGeoLocationDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionAccessNetworkIdentifier(
                                        new IpV6AccessNetworkIdentifierSubOptions(new IpV6AccessNetworkIdentifierSubOptionGeoLocation(0, 0))))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6AccessNetworkIdentifierSubOptionOperatorIdentifierDataTooShort()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                    {
                        ExtensionHeaders =
                            new IpV6ExtensionHeaders(
                            new IpV6ExtensionHeaderMobilityBindingError(
                                IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                                new IpV6MobilityOptions(
                                    new IpV6MobilityOptionAccessNetworkIdentifier(
                                        new IpV6AccessNetworkIdentifierSubOptions(
                                            new IpV6AccessNetworkIdentifierSubOptionOperatorIdentifier(
                                                IpV6AccessNetworkIdentifierOperatorIdentifierType.PrivateEnterpriseNumber, DataSegment.Empty))))))
                    });
            Assert.IsTrue(packet.IsValid);
            --packet.Buffer[14 + 40 + 24 + 2 + 1];
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6AccessNetworkIdentifierDataTooShortForReadingSubOption()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(
                                new IpV6MobilityOptionAccessNetworkIdentifier(
                                    new IpV6AccessNetworkIdentifierSubOptions(
                                        new IpV6AccessNetworkIdentifierSubOptionOperatorIdentifier(
                                            IpV6AccessNetworkIdentifierOperatorIdentifierType.PrivateEnterpriseNumber, DataSegment.Empty))))))
                });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 24 + 1] -= 2;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }

        [TestMethod]
        public void IpV6AccessNetworkIdentifierDataTooShortFullSubOption()
        {
            Packet packet = PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer(),
                new IpV6Layer
                {
                    ExtensionHeaders =
                        new IpV6ExtensionHeaders(
                        new IpV6ExtensionHeaderMobilityBindingError(
                            IpV4Protocol.Skip, 0, IpV6BindingErrorStatus.UnrecognizedMhTypeValue, IpV6Address.Zero,
                            new IpV6MobilityOptions(
                                new IpV6MobilityOptionAccessNetworkIdentifier(
                                    new IpV6AccessNetworkIdentifierSubOptions(
                                        new IpV6AccessNetworkIdentifierSubOptionOperatorIdentifier(
                                            IpV6AccessNetworkIdentifierOperatorIdentifierType.PrivateEnterpriseNumber, new DataSegment(new byte[10])))))))
                });
            Assert.IsTrue(packet.IsValid);
            packet.Buffer[14 + 40 + 24 + 1] -= 5;
            Packet invalidPacket = new Packet(packet.Buffer, DateTime.Now, DataLinkKind.Ethernet);
            Assert.IsFalse(invalidPacket.IsValid);
        }
    }
}
