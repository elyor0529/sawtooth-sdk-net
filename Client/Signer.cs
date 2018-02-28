﻿using System.Linq;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace Sawtooth.Sdk.Client
{
    public class Signer
    {
        readonly static X9ECParameters Secp256k1 = ECNamedCurveTable.GetByName("secp256k1");
        readonly static ECDomainParameters DomainParams = new ECDomainParameters(Secp256k1.Curve, Secp256k1.G, Secp256k1.N, Secp256k1.H);
        readonly IDigest Sha256Digest = new Sha256Digest();

        readonly ECPrivateKeyParameters PrivateKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Sawtooth.Sdk.Client.Signer"/> class and generates new private key
        /// </summary>
        public Signer() : this(GeneratePrivateKey())
        {
            
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Sawtooth.Sdk.Client.Signer"/> class with a given private key
        /// </summary>
        /// <param name="privateKey">Private key.</param>
        public Signer(byte[] privateKey)
        {
            PrivateKey = new ECPrivateKeyParameters(new BigInteger(1, privateKey), DomainParams);
        }

        /// <summary>
        /// Generates random private key
        /// </summary>
        /// <returns>The private key.</returns>
        public static byte[] GeneratePrivateKey()
        {
            var keyParams = new ECKeyGenerationParameters(DomainParams, new SecureRandom());

            var generator = new ECKeyPairGenerator("ECDSA");
            generator.Init(keyParams);

            var keyPair = generator.GenerateKeyPair();
            return (keyPair.Private as ECPrivateKeyParameters).D.ToByteArray();
        }

        /// <summary>
        /// Sign the specified message with the associated private key
        /// </summary>
        /// <returns>The sign.</returns>
        /// <param name="digest">Digest.</param>
        public byte[] Sign(byte[] digest)
        {
            var signer = new ECDsaSigner(new HMacDsaKCalculator(new Sha256Digest()));
            signer.Init(true, PrivateKey);
            var signature = signer.GenerateSignature(digest);

            var R = signature[0];
            var S = signature[1];

            // Ensure low S
            if (!(S.CompareTo(Secp256k1.N.ShiftRight(1)) <= 0))
            {
                S = Secp256k1.N.Subtract(S);
            }

            return R.ToByteArrayUnsigned().Concat(S.ToByteArrayUnsigned()).ToArray();
        }

        public static bool Verify(byte[] digest, byte[] signature, byte[] publicKey)
        {
            var X = new BigInteger(1, publicKey.Skip(1).Take(32).ToArray());
            var Y = new BigInteger(1, publicKey.Skip(33).Take(32).ToArray());
            var point = Secp256k1.Curve.CreatePoint(X, Y);

            var R = new BigInteger(1, signature.Take(32).ToArray());
            var S = new BigInteger(1, signature.Skip(32).ToArray());

            var signer = new ECDsaSigner(new HMacDsaKCalculator(new Sha256Digest()));
            signer.Init(false, new ECPublicKeyParameters(point, DomainParams));
            return signer.VerifySignature(digest, R, S);
        }

        /// <summary>
        /// Returns the public key from the private key
        /// </summary>
        /// <returns>The public key.</returns>
        public byte[] GetPublicKey()
        {
            var Q = Secp256k1.G.Multiply(PrivateKey.D);
            return new ECPublicKeyParameters(Q, DomainParams).Q.Normalize().GetEncoded();
        }

        /// <summary>
        /// Returns the pirvate key associated with this instance
        /// </summary>
        /// <returns>The private key.</returns>
        public byte[] GetPrivateKey() => PrivateKey.D.ToByteArray();
    }
}