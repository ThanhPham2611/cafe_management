using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class SaveEnvelopeTests
    {
        private static SaveData Parse(byte[] raw, out SaveStatus st)
        {
            SaveData d;
            st = SaveEnvelope.Parse(raw, out d);
            return d;
        }

        private static byte[] Utf8(string s) { return new UTF8Encoding(false).GetBytes(s); }

        [Test]
        public void WrapParse_RoundTrip_PreservesFields()
        {
            byte[] file = SaveEnvelope.Wrap(SaveFixtures.V3);
            SaveStatus st;
            SaveData d = Parse(file, out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Ok));
            Assert.That(d.xu, Is.EqualTo(5400));
            Assert.That(d.shopName, Is.EqualTo("Quán Cà Phê Việt"));
            Assert.That(d.staff[0].name, Is.EqualTo("Lan"));
            Assert.That(d.layout[0].cell.y, Is.EqualTo(3));
        }

        [Test]
        public void Parse_BareLegacyJson_V1_V2_V3_Ok()
        {
            foreach (string json in new[] { SaveFixtures.V1, SaveFixtures.V2, SaveFixtures.V3 })
            {
                SaveStatus st;
                SaveData d = Parse(Utf8(json), out st);
                Assert.That(st, Is.EqualTo(SaveStatus.Ok));
                Assert.That(d, Is.Not.Null);
            }
        }

        [Test]
        public void Parse_BareWithBomAndLeadingWhitespace_Ok()
        {
            byte[] raw = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'\n', (byte)' ' }.Concat(Utf8(SaveFixtures.V3)).ToArray();
            SaveStatus st;
            Parse(raw, out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Ok));
        }

        [Test]
        public void Parse_EmptyWhitespaceAndNulFilled_Corrupt()
        {
            SaveStatus st;
            Parse(new byte[0], out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt));
            Parse(Utf8("   \n\t"), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt));
            Parse(new byte[256], out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt), "file toàn NUL sau khi hệ thống ngắt giữa lúc ghi");
            Parse(null, out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt));
        }

        [Test]
        public void Parse_TruncatedEnvelope_Corrupt()
        {
            byte[] file = SaveEnvelope.Wrap(SaveFixtures.V3);
            foreach (int cut in new[] { 5, 20, 40, 90, file.Length - 1, file.Length / 2 })
            {
                SaveStatus st;
                Parse(file.Take(cut).ToArray(), out st);
                Assert.That(st, Is.EqualTo(SaveStatus.Corrupt), "cut=" + cut);
            }
        }

        [Test]
        public void Parse_TruncatedBare_Corrupt()
        {
            string json = SaveFixtures.V3;
            SaveStatus st;
            Parse(Utf8(json.Substring(0, json.Length / 2)), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt));
        }

        [Test]
        public void Parse_FlippedBodyByte_Corrupt()
        {
            byte[] file = SaveEnvelope.Wrap(SaveFixtures.V3);
            file[file.Length - 20] ^= 0x01;
            SaveStatus st;
            Parse(file, out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt));
        }

        [Test]
        public void Parse_WrongLengthField_Corrupt()
        {
            byte[] file = SaveEnvelope.Wrap(SaveFixtures.V3);
            string s = Encoding.UTF8.GetString(file);
            string bad = s.Replace(SaveEnvelope.Magic + "1 ", SaveEnvelope.Magic + "1 9");
            SaveStatus st;
            Parse(Utf8(bad), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt));
        }

        [Test]
        public void Parse_FutureVersion_BareAndEnveloped()
        {
            SaveStatus st;
            Parse(Utf8(SaveFixtures.Future()), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.FutureVersion));
            Parse(SaveEnvelope.Wrap(SaveFixtures.Future()), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.FutureVersion));
        }

        [Test]
        public void Parse_NewerEnvelopeFormat_FutureVersion()
        {
            SaveStatus st;
            Parse(Utf8("CAFESAVE2 10 " + new string('a', 64) + "\n0123456789"), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.FutureVersion));
        }

        [Test]
        public void Parse_BracesArraysAndVersionZero_Corrupt()
        {
            SaveStatus st;
            Parse(Utf8("{}"), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt), "{} không được đọc thành save mặc định rỗng");
            Parse(Utf8("[1,2,3]"), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt));
            Parse(Utf8("{\"version\":0,\"xu\":5}"), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Corrupt));
        }

        [Test]
        public void Wrap_VietnameseText_ByteLengthDiffersFromCharLength_RoundTrips()
        {
            string json = SaveFixtures.V3;
            Assert.That(Utf8(json).Length, Is.GreaterThan(json.Length));
            SaveStatus st;
            SaveData d = Parse(SaveEnvelope.Wrap(json), out st);
            Assert.That(st, Is.EqualTo(SaveStatus.Ok));
            Assert.That(d.shopName, Is.EqualTo("Quán Cà Phê Việt"));
        }

        [Test]
        public void SaveData_AllFieldsAppearInV3Fixture()
        {
            // Thêm trường vào SaveData mà quên cập nhật SaveFixtures.V3 thì test này nhắc.
            foreach (FieldInfo f in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(SaveFixtures.V3, Does.Contain("\"" + f.Name + "\":"), f.Name);
        }
    }
}
