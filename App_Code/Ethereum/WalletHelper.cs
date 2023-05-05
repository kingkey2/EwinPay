public static class WalletHelper
{
    public static Nethereum.Web3.Accounts.Account CreateWallet()
    {
        var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
        var pKey = ByteArrayToString(ecKey.GetPrivateKeyAsBytes());

        return new Nethereum.Web3.Accounts.Account(pKey);
    }

    public static string ByteArrayToString(byte[] ba)
    {
        System.Text.StringBuilder hex = new System.Text.StringBuilder(ba.Length * 2);
        foreach (byte b in ba)
            hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }
}