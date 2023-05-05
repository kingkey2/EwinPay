using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;

/// <summary>
/// RSAUtil 的摘要描述
/// </summary>
public class RSAUtil {
    public RSAUtil() {
        //
        // TODO: 在這裡新增建構函式邏輯
        //
    }

    #region 私鑰加密部分
    public string Sign(string content, string privateKey, string input_charset) {
        byte[] Data = Encoding.GetEncoding(input_charset).GetBytes(content);
        RSACryptoServiceProvider rsa = DecodePemPrivateKey(privateKey);
        SHA1 sh = new SHA1CryptoServiceProvider();
        byte[] signData = rsa.SignData(Data, sh);


        string str = Convert.ToBase64String(signData);

        return str;
    }

    public bool Verify(string content, string signedString, string publicKey, string input_charset) {
        signedString = signedString.Replace("*", "+");
        signedString = signedString.Replace("-", "/");


        bool result = false;
        byte[] Data = Encoding.GetEncoding(input_charset).GetBytes(content);
        byte[] data = Convert.FromBase64String(signedString);
        RSAParameters paraPub = ConvertFromPublicKey(publicKey);
        RSACryptoServiceProvider rsaPub = new RSACryptoServiceProvider();
        rsaPub.ImportParameters(paraPub);
        SHA1 sh = new SHA1CryptoServiceProvider();
        result = rsaPub.VerifyData(Data, sh, data);
        return result;
    }

    private RSACryptoServiceProvider DecodePemPrivateKey(String pemstr) {
        byte[] pkcs8privatekey;
        pkcs8privatekey = Convert.FromBase64String(pemstr);
        if (pkcs8privatekey != null) {
            RSACryptoServiceProvider rsa = DecodePrivateKeyInfo(pkcs8privatekey);
            return rsa;
        } else
            return null;
    }

    //------- Parses binary asn.1 PKCS #8 PrivateKeyInfo; returns RSACryptoServiceProvider ---
    private RSACryptoServiceProvider DecodePrivateKeyInfo(byte[] pkcs8) {
        // encoded OID sequence for  PKCS #1 rsaEncryption szOID_RSA_RSA = "1.2.840.113549.1.1.1"
        // this byte[] includes the sequence byte and terminal encoded null 
        byte[] SeqOID = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
        byte[] seq = new byte[15];
        // ---------  Set up stream to read the asn.1 encoded SubjectPublicKeyInfo blob  ------
        MemoryStream mem = new MemoryStream(pkcs8);
        int lenstream = (int)mem.Length;
        BinaryReader binr = new BinaryReader(mem);    //wrap Memory Stream with BinaryReader for easy reading
        byte bt = 0;
        ushort twobytes = 0;

        try {

            twobytes = binr.ReadUInt16();
            if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
                binr.ReadByte();    //advance 1 byte
            else if (twobytes == 0x8230)
                binr.ReadInt16();   //advance 2 bytes
            else
                return null;


            bt = binr.ReadByte();
            if (bt != 0x02)
                return null;

            twobytes = binr.ReadUInt16();

            if (twobytes != 0x0001)
                return null;

            seq = binr.ReadBytes(15);       //read the Sequence OID
            if (!CompareBytearrays(seq, SeqOID))    //make sure Sequence for OID is correct
                return null;

            bt = binr.ReadByte();
            if (bt != 0x04) //expect an Octet string 
                return null;

            bt = binr.ReadByte();       //read next byte, or next 2 bytes is  0x81 or 0x82; otherwise bt is the byte count
            if (bt == 0x81)
                binr.ReadByte();
            else
             if (bt == 0x82)
                binr.ReadUInt16();
            //------ at this stage, the remaining sequence should be the RSA private key

            byte[] rsaprivkey = binr.ReadBytes((int)(lenstream - mem.Position));
            RSACryptoServiceProvider rsacsp = DecodeRSAPrivateKey(rsaprivkey);
            return rsacsp;
        } catch (Exception) {
            return null;
        } finally { binr.Close(); }

    }

    //------- Parses binary ans.1 RSA private key; returns RSACryptoServiceProvider  ---
    private RSACryptoServiceProvider DecodeRSAPrivateKey(byte[] privkey) {
        byte[] MODULUS, E, D, P, Q, DP, DQ, IQ;

        // ---------  Set up stream to decode the asn.1 encoded RSA private key  ------
        MemoryStream mem = new MemoryStream(privkey);
        BinaryReader binr = new BinaryReader(mem);    //wrap Memory Stream with BinaryReader for easy reading
        byte bt = 0;
        ushort twobytes = 0;
        int elems = 0;
        try {
            twobytes = binr.ReadUInt16();
            if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
                binr.ReadByte();    //advance 1 byte
            else if (twobytes == 0x8230)
                binr.ReadInt16();   //advance 2 bytes
            else
                return null;

            twobytes = binr.ReadUInt16();
            if (twobytes != 0x0102) //version number
                return null;
            bt = binr.ReadByte();
            if (bt != 0x00)
                return null;


            //------  all private key components are Integer sequences ----
            elems = GetIntegerSize(binr);
            MODULUS = binr.ReadBytes(elems);

            elems = GetIntegerSize(binr);
            E = binr.ReadBytes(elems);

            elems = GetIntegerSize(binr);
            D = binr.ReadBytes(elems);

            elems = GetIntegerSize(binr);
            P = binr.ReadBytes(elems);

            elems = GetIntegerSize(binr);
            Q = binr.ReadBytes(elems);

            elems = GetIntegerSize(binr);
            DP = binr.ReadBytes(elems);

            elems = GetIntegerSize(binr);
            DQ = binr.ReadBytes(elems);

            elems = GetIntegerSize(binr);
            IQ = binr.ReadBytes(elems);

            // ------- create RSACryptoServiceProvider instance and initialize with public key -----
            RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
            RSAParameters RSAparams = new RSAParameters();
            RSAparams.Modulus = MODULUS;
            RSAparams.Exponent = E;
            RSAparams.D = D;
            RSAparams.P = P;
            RSAparams.Q = Q;
            RSAparams.DP = DP;
            RSAparams.DQ = DQ;
            RSAparams.InverseQ = IQ;
            RSA.ImportParameters(RSAparams);
            return RSA;
        } catch (Exception) {
            return null;
        } finally { binr.Close(); }
    }

    private int GetIntegerSize(BinaryReader binr) {
        byte bt = 0;
        byte lowbyte = 0x00;
        byte highbyte = 0x00;
        int count = 0;
        bt = binr.ReadByte();
        if (bt != 0x02)     //expect integer
            return 0;
        bt = binr.ReadByte();

        if (bt == 0x81)
            count = binr.ReadByte();    // data size in next byte
        else
        if (bt == 0x82) {
            highbyte = binr.ReadByte(); // data size in next 2 bytes
            lowbyte = binr.ReadByte();
            byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
            count = BitConverter.ToInt32(modint, 0);
        } else {
            count = bt;     // we already have the data size
        }



        while (binr.ReadByte() == 0x00) {   //remove high order zeros in data
            count -= 1;
        }
        binr.BaseStream.Seek(-1, SeekOrigin.Current);       //last ReadByte wasn't a removed zero, so back up a byte
        return count;
    }

    private bool CompareBytearrays(byte[] a, byte[] b) {
        if (a.Length != b.Length)
            return false;
        int i = 0;
        foreach (byte c in a) {
            if (c != b[i])
                return false;
            i++;
        }
        return true;
    }


    #endregion


    #region 公鑰加密 私鑰解密

    public string RSA_Encrypt(string DataString, string publicKey, bool Base64Encoding = true) {
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();
        byte[] totalHash = RSA_Encrypt(System.Text.Encoding.UTF8.GetBytes(DataString), publicKey);

        if (Base64Encoding) {
            RetValue.Append(System.Convert.ToBase64String(totalHash));
        } else {
            foreach (byte EachByte in totalHash) {
                // => .ToString("x2")
                string ByteStr = EachByte.ToString("x");

                ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
                RetValue.Append(ByteStr);
            }
        }

        return RetValue.ToString();
    }

    public string RSA_Encrypt_ByLCPay(string DataString, string publicKey, bool Base64Encoding = true) {
        byte[] Data = System.Text.Encoding.UTF8.GetBytes(DataString);
        byte[] totalHash;
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();

        int Count = Data.Length / 117;

        if ((Data.Length - (Count * 117)) > 0) {
            Count++;
        }

        totalHash = new byte[Count * 128];

        for (int i = 0 ; i < Count ; i++) {
            byte[] partData = Data.Skip(i * 117).Take(117).ToArray();
            byte[] hash = RSA_Encrypt(partData, publicKey);
            Array.Copy(hash, 0, totalHash, i * 128, 128);

        }


        if (Base64Encoding) {
            RetValue.Append(System.Convert.ToBase64String(totalHash));
        } else {
            foreach (byte EachByte in totalHash) {
                // => .ToString("x2")
                string ByteStr = EachByte.ToString("x");

                ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
                RetValue.Append(ByteStr);
            }
        }

        return RetValue.ToString();
    }

    public string RSA_Encrypt_Byjeepay(string rawInput, string publicKey)
    {
        if (string.IsNullOrEmpty(rawInput))
        {
            return string.Empty;
        }
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new ArgumentException("Invalid Public Key");
        }
        using (var rsaProvider = new RSACryptoServiceProvider())
        {
            var inputBytes = Encoding.UTF8.GetBytes(rawInput);//有含义的字符串转化为字节流
            rsaProvider.FromXmlString(publicKey);//载入公钥
            int bufferSize = (rsaProvider.KeySize / 8) - 11;//单块最大长度
            var buffer = new byte[bufferSize];
            using (MemoryStream inputStream = new MemoryStream(inputBytes), outputStream = new MemoryStream())
            {
                while (true)
                {
                    //分段加密
                    int readSize = inputStream.Read(buffer, 0, bufferSize);
                    if (readSize <= 0)
                    {
                        break;
                    }

                    var temp = new byte[readSize];
                    Array.Copy(buffer, 0, temp, 0, readSize);
                    var encryptedBytes = rsaProvider.Encrypt(temp, false);
                    outputStream.Write(encryptedBytes, 0, encryptedBytes.Length);
                }
                return Convert.ToBase64String(outputStream.ToArray());//转化为字节流方便传输
            }
        }
    }

    public string RSA_SingData_Byjeepay2(string rawInput, string publicKey)
    {
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(publicKey);
            byte[] Data1 = Encoding.GetEncoding("utf-8").GetBytes(rawInput);
            byte[] signedBytes1 = rsa.SignData(Data1, CryptoConfig.MapNameToOID("SHA256"));
            var sign = BitConverter.ToString(signedBytes1).Replace("-", "").ToLower();
            return sign;
        }
    }

    public string RSA_Decrypt_Byjeepay2(string sdata, string prikey)
    {
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(prikey);
            byte[] encryptedData = GetStringToBytes(sdata);

            int keysize = rsa.KeySize / 8;
            byte[] buffer = new byte[keysize];
            using (MemoryStream input = new MemoryStream(encryptedData))
            {
                using (MemoryStream output = new MemoryStream())
                {
                    while (true)
                    {
                        int readLine = input.Read(buffer, 0, keysize);
                        if (readLine <= 0)
                        {
                            break; // TODO: might not be correct. Was : Exit While
                        }
                        byte[] temp = new byte[readLine];
                        Array.Copy(buffer, 0, temp, 0, readLine);
                        byte[] decrypt = rsa.Decrypt(temp, false);
                        output.Write(decrypt, 0, decrypt.Length);
                    }
                    return System.Text.Encoding.UTF8.GetString(output.ToArray());
                    //得到解密结果
                }
            }

        }
    }
    
    private static byte[] GetStringToBytes(string value)
    {
        System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary shb = System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary.Parse(value);
        return shb.Value;
    }


    public string RSA_SingData_Byjeepay(string rawInput, string publicKey)
    {
        if (string.IsNullOrEmpty(rawInput))
        {
            return string.Empty;
        }
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new ArgumentException("Invalid Public Key");
        }
        using (var rsaProvider = new RSACryptoServiceProvider())
        {
            var inputBytes = Encoding.UTF8.GetBytes(rawInput);//有含义的字符串转化为字节流
            rsaProvider.FromXmlString(publicKey);//载入公钥
            int bufferSize = (rsaProvider.KeySize / 8) - 11;//单块最大长度
            var buffer = new byte[bufferSize];

            byte[] messagebytes = Encoding.UTF8.GetBytes(rawInput);
            //byte[] signData = rsa.SignData(Data, sh);
            var encryptedBytes = rsaProvider.SignData(messagebytes, "SHA1");
            return Convert.ToBase64String(encryptedBytes.ToArray());//转化为字节流方便传输

        }
    }

    public string RSA_SingData_fifty(string rawInput, string publicKey)
    {
        if (string.IsNullOrEmpty(rawInput))
        {
            return string.Empty;
        }
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new ArgumentException("Invalid Public Key");
        }
        using (var rsaProvider = new RSACryptoServiceProvider())
        {
            var inputBytes = Encoding.UTF8.GetBytes(rawInput);//有含义的字符串转化为字节流
            rsaProvider.FromXmlString(publicKey);//载入公钥
            int bufferSize = (rsaProvider.KeySize / 8) - 11;//单块最大长度
            var buffer = new byte[bufferSize];

            byte[] messagebytes = Encoding.UTF8.GetBytes(rawInput);
            //byte[] signData = rsa.SignData(Data, sh);
            var encryptedBytes = rsaProvider.SignData(messagebytes, "SHA1");
            return Convert.ToBase64String(encryptedBytes.ToArray());//转化为字节流方便传输

        }
    }
    

    public byte[] RSA_Encrypt(byte[] Data, string publicKey) {

        byte[] hash;
        RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
        RSA.FromXmlString(publicKey);

        //fOAEP
        //Type: System.Boolean

        //true to perform direct RSA encryption using OAEP padding (only available on a
        //computer running Microsoft Windows XP or later); otherwise, false to use PKCS#1 v1.5 padding. 
        hash = RSA.Encrypt(Data, false);

        return hash;
    }

    public string RSA_Decrypt(string DataString, string privateKey, bool Base64Encoding = true) {
        byte[] Data;

        if (Base64Encoding) {
            Data = System.Convert.FromBase64String(DataString);
        } else {
            // x2 => byte[]
            Data = new byte[DataString.Length / 2];
            for (var x = 0 ; x < DataString.Length / 2 ; x++) {
                var i = (Convert.ToInt32(DataString.Substring(x * 2, 2), 16));
                Data[x] = (byte)i;
            }
        }


        return RSA_Decrypt(Data, privateKey);
    }

    public string RSA_Decrypt_ByLCPay(string DataString, string privateKey, bool Base64Encoding = true) {
        byte[] Data;
        string RetValue = "";

        if (Base64Encoding) {
            Data = System.Convert.FromBase64String(DataString);
        } else {
            // x2 => byte[]
            Data = new byte[DataString.Length / 2];
            for (var x = 0 ; x < DataString.Length / 2 ; x++) {
                var i = (Convert.ToInt32(DataString.Substring(x * 2, 2), 16));
                Data[x] = (byte)i;
            }
        }


        int Count = Data.Length / 128;

        if ((Data.Length - (Count * 128)) > 0) {
            Count++;
        }

        for (int i = 0 ; i < Count ; i++) {
            byte[] partData = Data.Skip(i * 128).Take(128).ToArray();

            RetValue += RSA_Decrypt(partData, privateKey);
        }

        return RetValue;
    }

    public string RSA_Decrypt_Byjeepay(string encryptedInput, string privateKey)
    {
        if (string.IsNullOrEmpty(encryptedInput))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("Invalid Private Key");
        }

        using (var rsaProvider = new RSACryptoServiceProvider())
        {
            var inputBytes = Convert.FromBase64String(encryptedInput);
            rsaProvider.FromXmlString(privateKey);
            int bufferSize = rsaProvider.KeySize / 8;
            var buffer = new byte[bufferSize];
            using (MemoryStream inputStream = new MemoryStream(inputBytes),
                 outputStream = new MemoryStream())
            {
                while (true)
                {
                    int readSize = inputStream.Read(buffer, 0, bufferSize);
                    if (readSize <= 0)
                    {
                        break;
                    }

                    var temp = new byte[readSize];
                    Array.Copy(buffer, 0, temp, 0, readSize);
                    var rawBytes = rsaProvider.Decrypt(temp, false);
                    outputStream.Write(rawBytes, 0, rawBytes.Length);
                }
                return Encoding.UTF8.GetString(outputStream.ToArray());
            }
        }
    }

    public string RSA_Decrypt(byte[] Data, string privateKey) {
        byte[] hash;
        RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
        RSA.FromXmlString(privateKey);


        //fOAEP
        //Type: System.Boolean

        //true to perform direct RSA encryption using OAEP padding (only available on a
        //computer running Microsoft Windows XP or later); otherwise, false to use PKCS#1 v1.5 padding. 
        hash = RSA.Decrypt(Data, false);
        return System.Text.Encoding.UTF8.GetString(hash);
    }

    public bool VerifyData(string originalMessage, string signedMessage, string publicKey)
    {
        var rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(publicKey);


        byte[] bytesToVerify = Convert.FromBase64String(originalMessage);
        byte[] signedBytes = Convert.FromBase64String(signedMessage);

        //SHA256Managed Hash = new SHA256Managed();
        //byte[] hashedData = Hash.ComputeHash(signedBytes);


        //bool success = rsa.VerifyData(bytesToVerify, CryptoConfig.MapNameToOID("SHA1"), signedBytes);  
        bool success = rsa.VerifyData(bytesToVerify, CryptoConfig.MapNameToOID("SHA1"), signedBytes);


        bool Verified = rsa.VerifyData(
                    UTF8Encoding.UTF8.GetBytes(originalMessage),
                    new SHA1CryptoServiceProvider(),
                    Convert.FromBase64String(signedMessage));

        return Verified;
    }
    #endregion

    //私钥签名
    public string RSASignWithPrivateKeyForPortal(string signStr, string privateKey)
    {
        try
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            RSAParameters para = new RSAParameters();
            rsa.FromXmlString(privateKey);
            byte[] signBytes = rsa.SignData(UTF8Encoding.UTF8.GetBytes(signStr), "md5");
            return Convert.ToBase64String(signBytes);
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public string RSASignWithPrivateKeyForMa1w4d(string signStr, string privateKey)
    {
        try
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            RSAParameters para = new RSAParameters();
            rsa.FromXmlString(privateKey);
            byte[] signBytes = rsa.SignData(UTF8Encoding.UTF8.GetBytes(signStr), "SHA256");

            return Convert.ToBase64String(signBytes).Replace('+', '-')
              .Replace('/', '_')
              .Replace("=", "");
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public  string RSASignWithPrivateKeyForFiftyseven(string xmlPrivateKey, string signStr)
    {
        System.Security.Cryptography.SHA256 SHA256Provider = new System.Security.Cryptography.SHA256CryptoServiceProvider();
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();
        var Data = System.Text.Encoding.UTF8.GetBytes(signStr);
        var signSHA256byte = SHA256Provider.ComputeHash(Data);

        using (RSA rsa = RSA.Create())
        {
            //The hash to sign.

            rsa.FromXmlString(xmlPrivateKey);
            //Create an RSASignatureFormatter object and pass it the 
            //RSA instance to transfer the key information.
            RSAPKCS1SignatureFormatter RSAFormatter = new RSAPKCS1SignatureFormatter(rsa);

            //Set the hash algorithm to SHA256.
            RSAFormatter.SetHashAlgorithm("SHA256");

            //Create a signature for HashValue and return it.
            byte[] SignedHash = RSAFormatter.CreateSignature(signSHA256byte);

            return Convert.ToBase64String(SignedHash).Replace('+', '-').Replace('/', '_');
        }
    }

    /// <summary>
    /// 签名验证
    /// </summary>
    /// <param name="str">待验证的字符串</param>
    /// <param name="sign">加签之后的字符串</param>
    /// <param name="publicKey">公钥</param>
    /// <param name="encoding">编码格式</param>
    /// <returns>签名是否符合</returns>
    public  bool RSASignCheckPublicKeyForFiftyseven(string str, string sign, string publicKey, string encoding)
    {
        try
        {
            byte[] bt = Encoding.GetEncoding(encoding).GetBytes(str);
            var sha256 = new SHA256CryptoServiceProvider();
            byte[] rgbHash = sha256.ComputeHash(bt);

            RSACryptoServiceProvider key = new RSACryptoServiceProvider();
            key.FromXmlString(publicKey);
            RSAPKCS1SignatureDeformatter deformatter = new RSAPKCS1SignatureDeformatter(key);
            deformatter.SetHashAlgorithm("SHA256");
            byte[] rgbSignature = Convert.FromBase64String(sign);
            if (deformatter.VerifySignature(rgbHash, rgbSignature))
            {
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    //使用公钥验签
    public  bool RSAValidateSignForPortal(string plainText, string publicKey, string signedData)
    {
        try
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            RSAParameters para = new RSAParameters();
            rsa.FromXmlString(publicKey);
            return rsa.VerifyData(UTF8Encoding.UTF8.GetBytes(plainText), "md5", Convert.FromBase64String(signedData));
        }
        catch (Exception e)
        {
            throw e;
        }
    }


    #region x955
    public string EncryptForX955(string publicKey, string content)
    {
        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(publicKey);

        var encryptString = Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(content), false));

        return encryptString;
    }

    public string DecryptForX955(string privateKey, string encryptedContent)
    {
        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(privateKey);

        var decryptString = Encoding.UTF8.GetString(rsa.Decrypt(Convert.FromBase64String(encryptedContent), false));

        return decryptString;
    } 
    #endregion

    #region 
    private RSAParameters ConvertFromPublicKey(string pemFileConent) {

        byte[] keyData = Convert.FromBase64String(pemFileConent);
        if (keyData.Length < 162) {
            throw new ArgumentException("pem file content is incorrect.");
        }
        byte[] pemModulus = new byte[128];
        byte[] pemPublicExponent = new byte[3];
        Array.Copy(keyData, 29, pemModulus, 0, 128);
        Array.Copy(keyData, 159, pemPublicExponent, 0, 3);
        RSAParameters para = new RSAParameters();
        para.Modulus = pemModulus;
        para.Exponent = pemPublicExponent;
        return para;
    }

    private RSAParameters ConvertFromPrivateKey(string pemFileConent) {
        byte[] keyData = Convert.FromBase64String(pemFileConent);
        if (keyData.Length < 609) {
            throw new ArgumentException("pem file content is incorrect.");
        }

        int index = 11;
        byte[] pemModulus = new byte[128];
        Array.Copy(keyData, index, pemModulus, 0, 128);

        index += 128;
        index += 2;//141
        byte[] pemPublicExponent = new byte[3];
        Array.Copy(keyData, index, pemPublicExponent, 0, 3);

        index += 3;
        index += 4;//148
        byte[] pemPrivateExponent = new byte[128];
        Array.Copy(keyData, index, pemPrivateExponent, 0, 128);

        index += 128;
        index += ((int)keyData[index + 1] == 64 ? 2 : 3);//279
        byte[] pemPrime1 = new byte[64];
        Array.Copy(keyData, index, pemPrime1, 0, 64);

        index += 64;
        index += ((int)keyData[index + 1] == 64 ? 2 : 3);//346
        byte[] pemPrime2 = new byte[64];
        Array.Copy(keyData, index, pemPrime2, 0, 64);

        index += 64;
        index += ((int)keyData[index + 1] == 64 ? 2 : 3);//412/413
        byte[] pemExponent1 = new byte[64];
        Array.Copy(keyData, index, pemExponent1, 0, 64);

        index += 64;
        index += ((int)keyData[index + 1] == 64 ? 2 : 3);//479/480
        byte[] pemExponent2 = new byte[64];
        Array.Copy(keyData, index, pemExponent2, 0, 64);

        index += 64;
        index += ((int)keyData[index + 1] == 64 ? 2 : 3);//545/546
        byte[] pemCoefficient = new byte[64];
        Array.Copy(keyData, index, pemCoefficient, 0, 64);

        RSAParameters para = new RSAParameters();
        para.Modulus = pemModulus;
        para.Exponent = pemPublicExponent;
        para.D = pemPrivateExponent;
        para.P = pemPrime1;
        para.Q = pemPrime2;
        para.DP = pemExponent1;
        para.DQ = pemExponent2;
        para.InverseQ = pemCoefficient;
        return para;
    }
    #endregion

    #region RunPay

    public string SignByXmlPrivateKey(string content, string privateKey, string input_charset, bool isBase64)
    {
        System.Text.StringBuilder RetValue = new System.Text.StringBuilder();
        byte[] Data = Encoding.GetEncoding(input_charset).GetBytes(content);
        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        SHA1 sh = new SHA1CryptoServiceProvider();
        rsa.FromXmlString(privateKey);

        byte[] signData = rsa.SignData(Data, sh);

        if (isBase64)
        {
            RetValue.Append(System.Convert.ToBase64String(signData));
        }
        else
        {
            foreach (byte EachByte in signData)
            {
                // => .ToString("x2")
                string ByteStr = EachByte.ToString("x");

                ByteStr = new string('0', 2 - ByteStr.Length) + ByteStr;
                RetValue.Append(ByteStr);
            }
        }

        return RetValue.ToString();
    }

    public bool VerifyByXmlPublicKey(string content, string signedString, string publicKey, string input_charset, bool isBase64)
    {

        bool result = false;
        byte[] Data = Encoding.GetEncoding(input_charset).GetBytes(content);
        byte[] data;
        if (isBase64)
        {
            data = Convert.FromBase64String(signedString);
        }
        else
        {
            // x2 => byte[]
            data = new byte[signedString.Length / 2];
            for (var x = 0; x < signedString.Length / 2; x++)
            {
                var i = (Convert.ToInt32(signedString.Substring(x * 2, 2), 16));
                Data[x] = (byte)i;
            }
        }

        RSACryptoServiceProvider rsaPub = new RSACryptoServiceProvider();

        rsaPub.FromXmlString(publicKey);

        SHA1 sh = new SHA1CryptoServiceProvider();
        result = rsaPub.VerifyData(Data, sh, data);
        return result;
    }

    public Tuple<string, string> CreateXmlKey()
    {
        var rsaEnc = new RSACryptoServiceProvider();
        var pubKey = rsaEnc.ToXmlString(false);
        var priKey = rsaEnc.ToXmlString(true);

        return new Tuple<string, string>(pubKey, priKey);
    }

    #endregion

    #region ma1
    public  string Sign_ma1(string contentForSign, string privateKey)
    {
        //轉換成適用於.Net的秘鑰
   
        var rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(privateKey);
        //創建一個空對象
        var rsaClear = new RSACryptoServiceProvider();
        var paras = rsa.ExportParameters(true);
        rsaClear.ImportParameters(paras);
        //簽名返回
        using (var sha256 = new SHA256CryptoServiceProvider())
        {
            var signData = rsa.SignData(Encoding.UTF8.GetBytes(contentForSign), sha256);
            return Convert.ToBase64String(signData);
        }
    }

    public  bool VerifySign_ma1(string contentForSign, string publicKey, string signedData)
    {
        var rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(publicKey);
        using (var sha256 = new SHA256CryptoServiceProvider())
        {
            return rsa.VerifyData(Encoding.UTF8.GetBytes(contentForSign), sha256, Convert.FromBase64String(signedData));
        }
    }
    public static string BytesToHex(byte[] data)
    {
        StringBuilder sbRet = new StringBuilder(data.Length * 2);
        for (int i = 0; i < data.Length; i++)
        {
            sbRet.Append(Convert.ToString(data[i], 16).PadLeft(2, '0'));
        }
        return sbRet.ToString();
    }

    public static byte[] HexToBytes(string text)
    {
        if (text.Length % 2 != 0)
            throw new ArgumentException("text 长度为奇数。");

        List<byte> lstRet = new List<byte>();
        for (int i = 0; i < text.Length; i = i + 2)
        {
            lstRet.Add(Convert.ToByte(text.Substring(i, 2), 16));
        }
        return lstRet.ToArray();
    }

    #endregion
}