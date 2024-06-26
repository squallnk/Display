﻿using Display.Helper.FileProperties.Name;
using System.Diagnostics;

namespace Tests
{
    [TestClass]
    public class SortTest
    {
        private readonly Dictionary<string, string> _sampleFileDictionary = new()
        {
            { "hhd1080.com@1nhdtb00259hhb.wtbjj0e3", "NHDTB-259" },
            { "hhd800_com@h_1605stsk00038_4Ks", "STSK-038" },
            { "hotavxxx.com_092411-815-carib-whole_hd1", "092411-815" },
            { "hhd800.com@cawd00302hhb", "CAWD-302" },
            { "hhd800.com@cjod00388hhb", "CJOD-388" },
            { "URE-031-C", "URE-031" },
            { "ciel-001", "CIEL-001" }
        };

        [TestMethod]
        public void MatchNameTest()
        {
            foreach (var (src, dst) in _sampleFileDictionary)
            {
                var destinationName = FileMatch.MatchName(src).ToUpper();
                Debug.WriteLine(destinationName);

                Assert.IsNotNull(destinationName, "未匹配：" + src);
                Assert.AreEqual(dst, destinationName, $"匹配出错：{src}");
            }
        }
    }
}
