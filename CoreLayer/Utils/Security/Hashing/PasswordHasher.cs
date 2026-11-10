
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CoreLayer.Security.Hashing;

/// <summary>
/// Kullanıcı parolalarını güvenli şekilde hash'lemek ve doğrulamak için
/// kullanılan merkezi yardımcı sınıftır.
///
/// Bu sınıf parola saklamak için geri döndürülebilir şifreleme (encryption)
/// kullanmaz. Bunun yerine tek yönlü parola hash'leme yaklaşımını kullanır.
///
/// Temel algoritma:
/// 1. Parolayı Unicode FormC ile normalize et.
/// 2. PBKDF2-SHA256 ile salt kullanarak türetilmiş anahtar oluştur.
/// 3. Türetilmiş anahtarı Pepper ile HMAC-SHA256 işleminden geçir.
/// 4. Salt ve sonucu versiyon bilgileriyle birlikte sakla.
///
/// Böylece veritabanı ele geçirilse bile saldırganın parolaları doğrudan
/// elde etmesi mümkün olmaz.
/// </summary>
public static class PasswordHasher
{
    // Hash formatının versiyonunu belirtir.
    // Format değişiklikleri yapıldığında eski hash'lerin de doğrulanabilmesini sağlar.
    private const int CurrentFormatVersion = 2;

    // Önceki hash formatının versiyonu.
    // Eski kullanıcıların parolalarını yeniden belirlemeden sisteme giriş
    // yapabilmelerini sağlamak için geriye dönük uyumluluk sunar.
    private const int LegacyFormatVersion = 1;

    // Pepper mekanizmasının versiyonudur.
    // İleride pepper değiştirilmesi gerekirse eski pepper versiyonlarını
    // tanımlayabilmek ve eski hash'leri doğrulayabilmek için kullanılır.
    private const int CurrentPepperVersion = 1;

    // PBKDF2 işleminin kaç iterasyon gerçekleştireceğini belirtir.
    //
    // İterasyon sayısı arttıkça parola türetme işlemi daha maliyetli hale gelir.
    // Bu durum saldırganların çok sayıda parola denemesini zorlaştırır.
    //
    // Aynı zamanda her giriş işleminde sunucuya ek işlem maliyeti getirir.
    // Bu nedenle güvenlik ve performans arasında dengeli bir değer seçilmelidir.
    private const int Iterations = 600_000;

    // Doğrulama sırasında kabul edilebilecek maksimum iterasyon sayısıdır.
    //
    // Hash içerisinde iterasyon sayısı saklandığı için saldırgan veritabanındaki
    // hash değerini değiştirerek sunucuya aşırı işlem yaptırmaya çalışabilir.
    //
    // Bu sınır, hash içerisindeki iterasyon değerinin kontrolsüz şekilde
    // yükseltilmesini engelleyerek kaynak tüketimi saldırılarına karşı koruma sağlar.
    private const int MaxAcceptedIterations = 2_000_000;

    // Salt değerinin byte cinsinden uzunluğudur.
    // Her parola için kriptografik olarak güvenli rastgele bir salt oluşturulur.
    private const int SaltSize = 16;

    // PBKDF2 ve HMAC işlemlerinin üreteceği hash çıktısının byte uzunluğudur.
    // SHA-256 algoritmasının çıktısı 256 bit, yani 32 byte'tır.
    private const int HashSize = 32;

    // Normalize edilmiş parolanın UTF-8 olarak maksimum uzunluğudur.
    // Aşırı uzun parolaların gereksiz bellek ve işlem tüketmesine karşı sınır koyar.
    private const int MaxPasswordBytes = 1024;

    // Pepper, salt'tan farklı olarak her parola için değişmez.
    //
    // Salt:
    // - Her parola hash'inde rastgele oluşturulur.
    // - Hash ile birlikte veritabanında saklanır.
    //
    // Pepper:
    // - Uygulamaya ait gizli bir anahtardır.
    // - Hash ile birlikte veritabanında saklanmaz.
    // - Aynı pepper versiyonunu kullanan hash'lerde ortak kullanılır.
    //
    // Bu değeri değiştirmek mevcut hash'lerin doğrulanmasını etkiler.
    // Bu nedenle deployment sonrasında mevcut değerin değiştirilmemesi gerekir.
    private const string PepperBase64 = "C1e1tFcfmFk73tNUDNR/HyYzOYHrL0qFioT61N0kxmA=";

    // Hesap bulunamadığında veya geçersiz parola durumunda gerçekleştirilen
    // dummy PBKDF2 işlemlerinde kullanılacak sabit salt değeridir.
    //
    // Burada rastgele değer üretmiyoruz. Amacımız gerçek bir parola hash'i
    // oluşturmak değil, gereksiz zaman farklarını azaltmaktır.
    private const string DummySaltHex = "A13F8C72D491E605B27A39F10C6D84E2";

    // Hex formatındaki dummy salt değerini byte dizisine dönüştürür.
    // Bu değer uygulama boyunca tekrar kullanılır.
    private static readonly byte[] DummySalt =
        Convert.FromHexString(DummySaltHex);

    // UTF-16 veya maksimum uzunluk kontrollerinden geçemeyen parolalar için
    // kullanılacak sabit bir placeholder değerdir.
    //
    // Örneğin:
    // - Geçersiz UTF-16 karakterleri içeren parola
    // - 1024 byte sınırını aşan parola
    //
    // Bu değer gerçek parola değildir. Yalnızca dummy işleminde kullanılır.
    private static readonly byte[] InvalidPasswordPlaceholder =
        Encoding.UTF8.GetBytes("invalid-password-placeholder");

    // Varsayılan UTF8Encoding, geçersiz karakterlerde replacement karakter
    // kullanabilir. Bu davranış parola doğrulamasında istenmez.
    //
    // throwOnInvalidBytes = true sayesinde geçersiz UTF-16 verileri UTF-8'e
    // dönüştürülürken hata oluşturulur.
    //
    // Böylece farklı ve geçersiz karakter dizilerinin aynı parolaya
    // dönüştürülmesi engellenir.
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// Verilen parolayı PBKDF2-SHA256 ve HMAC-SHA256 kullanarak hash'ler.
    ///
    /// Oluşturulan hash V2 formatındadır.
    /// Her işlemde yeni ve rastgele bir salt kullanılır.
    ///
    /// Aynı parola iki defa hash'lendiğinde farklı sonuçlar elde edilmesi normaldir.
    /// Bunun nedeni her işlemde farklı salt oluşturulmasıdır.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Parola null, boş, geçersiz UTF-16 içeriyor veya maksimum UTF-8 byte
    /// uzunluğunu aşıyorsa fırlatılır.
    /// </exception>
    public static string HashPassword(string plainPassword)
    {
        // Null veya boş parola kabul edilmez.
        // Bu kontrol kriptografik işlemler başlamadan önce yapılır.
        ArgumentException.ThrowIfNullOrEmpty(plainPassword);

        // Kriptografik işlemlerde kullanılacak byte dizileri başlangıçta
        // boş olarak tanımlanır.
        //
        // Bunları finally bloğunda temizleyeceğiz.
        byte[] passwordBytes = [];
        byte[] salt = [];
        byte[] derivedKey = [];
        byte[] pepper = [];
        byte[] hashOutput = [];

        try
        {
            // Parolayı önce Unicode FormC normalizasyonundan geçiriyoruz.
            // Ardından strict UTF-8 ile byte dizisine dönüştürüyoruz.
            //
            // Örneğin Unicode içerisinde görsel olarak aynı görünen bazı
            // karakterler farklı byte dizileriyle temsil edilebilir.
            // Normalizasyon bu farklılıkları standart hale getirir.
            //
            // Geçersiz UTF-16 varsa işlem false döndürür.
            if (!TryNormalizePassword(plainPassword, out passwordBytes))
            {
                throw new ArgumentException(
                    "Password contains invalid UTF-16 characters.",
                    nameof(plainPassword));
            }

            // Parolanın byte uzunluğunu kontrol ediyoruz.
            // Karakter sayısı ile byte sayısı aynı şey değildir.
            //
            // Örneğin UTF-8 içerisinde bazı karakterler birden fazla byte
            // kullanır. Bu nedenle kontrolü string.Length üzerinden yapmıyoruz.
            if (passwordBytes.Length > MaxPasswordBytes)
            {
                throw new ArgumentException(
                    $"Password cannot exceed {MaxPasswordBytes} UTF-8 bytes.",
                    nameof(plainPassword));
            }

            // Her parola hash'inde yeni, kriptografik olarak güvenli
            // ve tahmin edilemeyen bir salt oluşturulur.
            //
            // Aynı parolaya sahip iki kullanıcı böylece farklı hash değerleri alır.
            //
            // RandomNumberGenerator, güvenli rastgele sayı üretimi için
            // kullanılan .NET kriptografi API'sidir.
            salt = RandomNumberGenerator.GetBytes(SaltSize);

            // PBKDF2 algoritması ile paroladan türetilmiş bir anahtar oluşturulur.
            //
            // Parametreler:
            // passwordBytes : Normalize edilmiş parola.
            // salt          : Bu parola için oluşturulan rastgele salt.
            // Iterations    : PBKDF2 iterasyon sayısı.
            // SHA256        : Kullanılacak hash algoritması.
            // HashSize      : Üretilecek çıktının byte uzunluğu.
            //
            // PBKDF2, parolayı doğrudan SHA256 ile hash'lemekten daha maliyetlidir.
            // Çünkü belirlenen iterasyon sayısı kadar işlem gerçekleştirir.
            derivedKey = Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            // Kullanılacak pepper değerini elde ediyoruz.
            // GetCurrentPepper içerisinde pepper versiyonu da kontrol edilir.
            pepper = GetCurrentPepper();

            // PBKDF2 sonucunu ikinci bir kriptografik işlemden geçiriyoruz.
            //
            // HMAC-SHA256:
            // - Pepper değerini gizli anahtar olarak kullanır.
            // - derivedKey değerini işlenecek veri olarak kullanır.
            // - 32 byte uzunluğunda sonuç üretir.
            //
            // Böylece yalnızca veritabanındaki salt ve hash bilgilerine
            // sahip olan bir saldırgan, pepper olmadan doğrulama işlemini
            // gerçekleştiremez.
            hashOutput = HMACSHA256.HashData(pepper, derivedKey);

            // Oluşturulan hash bilgisini string formatına dönüştürüyoruz.
            //
            // V2 formatı:
            // formatVersion$iterations$pepperVersion$salt$hash
            //
            // Örnek yapısal gösterim:
            // 2$600000$1$SALT_HEX$HASH_HEX
            //
            // Salt ve hash hexadecimal formatta saklanır.
            // Sayısal değerlerde kültür bağımlı format oluşmaması için
            // InvariantCulture kullanılır.
            return string.Join(
                '$',
                CurrentFormatVersion.ToString(CultureInfo.InvariantCulture),
                Iterations.ToString(CultureInfo.InvariantCulture),
                CurrentPepperVersion.ToString(CultureInfo.InvariantCulture),
                Convert.ToHexString(salt),
                Convert.ToHexString(hashOutput));
        }
        finally
        {
            // finally bloğu, try içerisinde return veya exception oluşsa bile çalışır.
            //
            // Parola, salt, türetilmiş anahtar ve pepper gibi hassas byte
            // dizilerinin bellekte gereksiz yere tutulmasını azaltmak için
            // içeriklerini sıfırlıyoruz.
            //
            // Not: Bu işlem string gibi immutable nesnelerin bellekteki
            // kopyalarını temizleyebileceğimiz anlamına gelmez.
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(derivedKey);
            CryptographicOperations.ZeroMemory(pepper);
            CryptographicOperations.ZeroMemory(hashOutput);
        }
    }

    /// <summary>
    /// Kullanıcının verdiği parolanın saklanan hash ile eşleşip eşleşmediğini kontrol eder.
    ///
    /// Hem V1 hem de V2 hash formatlarını destekler.
    ///
    /// V1:
    /// PBKDF2-SHA256 sonucu doğrudan saklanır.
    ///
    /// V2:
    /// PBKDF2-SHA256 sonucu Pepper ile HMAC-SHA256 işleminden geçirilir.
    ///
    /// Geçersiz UTF-16, aşırı uzun parola veya bozuk hash durumunda false döndürür.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Parola veya hash null ya da boş olduğunda fırlatılır.
    /// </exception>
    public static bool VerifyPassword(string plainPassword, string passwordHash)
    {
        // Null veya boş değerlerin doğrulama işlemine girmesini engeller.
        ArgumentException.ThrowIfNullOrEmpty(plainPassword);
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        // Doğrulama boyunca kullanılacak hassas veriler.
        // İşlem sonunda finally içerisinde temizlenecekler.
        byte[] passwordBytes = [];
        byte[] salt = [];
        byte[] expectedHash = [];
        byte[] derivedKey = [];
        byte[] pepper = [];
        byte[] actualHash = [];

        try
        {
            // Kullanıcı parolasını hash oluştururken kullandığımız
            // normalizasyon yöntemiyle aynı şekilde normalize ediyoruz.
            //
            // Hash üretimi ve doğrulama sırasında farklı normalizasyon
            // kullanılması doğru parolanın bile doğrulanamamasına neden olabilir.
            //
            // Ayrıca maksimum byte uzunluğu kontrol edilir.
            if (!TryNormalizePassword(plainPassword, out passwordBytes)
                || passwordBytes.Length > MaxPasswordBytes)
            {
                // Normalizasyon başarısızsa veya parola sınırı aşıyorsa
                // kısmen oluşturulmuş byte dizisini temizliyoruz.
                CryptographicOperations.ZeroMemory(passwordBytes);

                // Geçersiz parola için sabit placeholder değerinin
                // bir kopyasını oluşturuyoruz.
                //
                // Önemli:
                // InvalidPasswordPlaceholder doğrudan kullanılsaydı
                // ZeroMemory ile static dizinin kendisini temizlemiş olurduk.
                // Clone() sayesinde yalnızca bu metoda ait kopya temizlenir.
                passwordBytes = (byte[])InvalidPasswordPlaceholder.Clone();

                // Gerçek parola yerine placeholder ile maliyetli bir PBKDF2
                // işlemi gerçekleştiriyoruz.
                //
                // Amaç, geçersiz parola girişlerinin çok hızlı sonlanması
                // nedeniyle oluşabilecek zamanlama farklılıklarını azaltmaktır.
                RunDummyDerivation(passwordBytes);

                return false;
            }

            // Veritabanında saklanan hash değerini '$' karakterine göre ayırıyoruz.
            //
            // V1: 4 parça
            // V2: 5 parça
            //
            // Her parçanın formatı TryParseHash içerisinde kontrol edilir.
            string[] parts = passwordHash.Split('$');

            // Hash formatının geçerli olup olmadığını kontrol ediyoruz.
            //
            // Bu metot:
            // - Format versiyonunu kontrol eder.
            // - İterasyon sayısını kontrol eder.
            // - Pepper versiyonunu kontrol eder.
            // - Salt uzunluğunu kontrol eder.
            // - Hash uzunluğunu kontrol eder.
            //
            // Parse işlemi başarısızsa gerçek doğrulama yapılmaz.
            if (!TryParseHash(
                    parts,
                    out int formatVersion,
                    out int iterations,
                    out int pepperVersion,
                    out salt,
                    out expectedHash))
            {
                // Bozuk veya desteklenmeyen hash formatlarında da
                // PBKDF2 maliyetine yakın bir işlem gerçekleştirilir.
                //
                // Böylece farklı hata senaryolarının süreleri arasındaki
                // farkın azaltılması hedeflenir.
                RunDummyDerivation(passwordBytes);

                return false;
            }

            // V2 formatında pepper kullanılır.
            // V1 formatında ise pepper bulunmadığından bu işlem yapılmaz.
            if (formatVersion == CurrentFormatVersion)
            {
                // Hash içerisindeki pepper versiyonuna göre doğru pepper
                // değerini elde etmeye çalışıyoruz.
                //
                // Şu an yalnızca CurrentPepperVersion desteklenmektedir.
                // Desteklenmeyen versiyonlarda null döndürülür.
                byte[]? resolvedPepper = GetPepper(pepperVersion);

                if (resolvedPepper is null)
                {
                    // Desteklenmeyen pepper versiyonuna sahip hash doğrulanamaz.
                    // Yine de zamanlama farkını azaltmak için dummy işlem yapılır.
                    RunDummyDerivation(passwordBytes);

                    return false;
                }

                // Doğrulama boyunca kullanılacak pepper değerini saklıyoruz.
                // Bu byte dizisi finally bloğunda temizlenecektir.
                pepper = resolvedPepper;
            }

            // Kullanıcının girdiği parola, hash içerisinde saklanan salt ve
            // iterasyon sayısıyla tekrar PBKDF2 işleminden geçirilir.
            //
            // Burada hash üretirken kullanılan iterasyon sayısını kullanıyoruz.
            // Çünkü eski V1 hash'leri daha düşük iterasyon sayısıyla oluşturulmuş
            // olabilir. Doğrulamada aynı parametreler kullanılmalıdır.
            derivedKey = Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            // Hash formatına göre doğrulama için kullanılacak sonucu belirliyoruz.
            //
            // V1:
            // PBKDF2 çıktısı doğrudan saklanan hash ile karşılaştırılır.
            //
            // V2:
            // PBKDF2 çıktısı aynı pepper ile HMAC-SHA256 işleminden geçirilir.
            //
            // V1'de actualHash ve derivedKey aynı byte dizisini referans eder.
            // Bu nedenle aşağıdaki finally bloğunda referans kontrolü yapıyoruz.
            actualHash = formatVersion == LegacyFormatVersion
                ? derivedKey
                : HMACSHA256.HashData(pepper, derivedKey);

            // İki hash değerini sabit zamanlı karşılaştırma yöntemiyle kontrol eder.
            //
            // Normal string veya byte dizisi karşılaştırmalarında bazı
            // uygulamalar ilk farklı byte bulunduğunda işlemi sonlandırabilir.
            // Bu durum karşılaştırma süresinden bilgi sızmasına neden olabilir.
            //
            // FixedTimeEquals, karşılaştırma sırasında içerik farklılıklarına
            // bağlı erken çıkış davranışını engellemeyi amaçlar.
            //
            // Eşleşiyorsa true, eşleşmiyorsa false döndürür.
            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        finally
        {
            // Doğrulama sırasında kullanılan hassas verileri temizliyoruz.
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(expectedHash);

            // V1 formatında actualHash ve derivedKey aynı nesneyi gösterir.
            //
            // Aynı byte dizisine iki defa ZeroMemory çağrısı yapmamak için
            // ReferenceEquals ile aynı nesne olup olmadıklarını kontrol ediyoruz.
            //
            // V2 formatında actualHash ayrı bir HMAC çıktısıdır.
            // Bu nedenle önce actualHash temizlenir, ardından derivedKey temizlenir.
            if (!ReferenceEquals(actualHash, derivedKey))
            {
                CryptographicOperations.ZeroMemory(actualHash);
            }

            CryptographicOperations.ZeroMemory(derivedKey);
            CryptographicOperations.ZeroMemory(pepper);
        }
    }

    /// <summary>
    /// Verilen hash değerinin mevcut hash yapılandırmasına uygun olup olmadığını kontrol eder.
    ///
    /// Bu metot parolanın doğru olup olmadığını kontrol etmez.
    /// Yalnızca hash formatını ve kullanılan parametreleri inceler.
    ///
    /// Örneğin:
    /// - V1 hash ise true döndürür.
    /// - Iterasyon sayısı eskiyse true döndürür.
    /// - Pepper versiyonu eskiyse true döndürür.
    /// - Hash formatı bozuksa true döndürür.
    /// - Güncel V2 yapılandırmasına sahipse false döndürür.
    ///
    /// Genellikle başarılı bir giriş işleminden sonra kullanılır.
    /// NeedsRehash true döndürürse parola tekrar hash'lenerek veritabanı güncellenebilir.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Hash null veya boş olduğunda fırlatılır.
    /// </exception>
    public static bool NeedsRehash(string passwordHash)
    {
        // Boş veya null hash değerlerini kabul etmiyoruz.
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        // Parse işlemi sırasında oluşturulacak byte dizileri.
        byte[] salt = [];
        byte[] expectedHash = [];

        try
        {
            // Hash değerini format parçalarına ayırıyoruz.
            string[] parts = passwordHash.Split('$');

            // Önce hash'in yapısal olarak geçerli olup olmadığını kontrol ediyoruz.
            if (!TryParseHash(
                    parts,
                    out int formatVersion,
                    out int iterations,
                    out int pepperVersion,
                    out salt,
                    out expectedHash))
            {
                // Bozuk bir hash güncel kabul edilemez.
                return true;
            }

            // Hash'in mevcut yapılandırmayla karşılaştırmasını yapıyoruz.
            //
            // Aşağıdaki durumlardan herhangi biri varsa yeniden hash gerekir:
            //
            // 1. Hash formatı güncel değilse.
            // 2. İterasyon sayısı mevcut ayardan farklıysa.
            // 3. Pepper versiyonu mevcut ayardan farklıysa.
            //
            // Burada parolanın kendisini bilmemize gerek yoktur.
            // Çünkü yalnızca hash metadata bilgilerini inceliyoruz.
            return formatVersion != CurrentFormatVersion
                   || iterations != Iterations
                   || pepperVersion != CurrentPepperVersion;
        }
        finally
        {
            // Parse sırasında oluşturulan byte dizilerini temizliyoruz.
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(expectedHash);
        }
    }

    /// <summary>
    /// Hesabın bulunmadığı durumlarda parola doğrulama işlemine benzer
    /// maliyetli bir işlem gerçekleştirir.
    ///
    /// Örneğin login işleminde:
    ///
    /// Kullanıcı varsa:
    /// VerifyPassword(parola, hash)
    ///
    /// Kullanıcı yoksa:
    /// VerifyPasswordDummy(parola)
    ///
    /// Böylece saldırganın yalnızca işlem süresini gözlemleyerek bir hesabın
    /// sistemde bulunup bulunmadığını anlaması zorlaştırılır.
    ///
    /// Bu yöntem timing attack riskini azaltmaya yardımcı olur.
    /// Tek başına bütün zamanlama farklılıklarını ortadan kaldırmaz.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Parola null veya boş olduğunda fırlatılır.
    /// </exception>
    public static void VerifyPasswordDummy(string plainPassword)
    {
        // Null veya boş parola için dummy işlem gerçekleştirmiyoruz.
        ArgumentException.ThrowIfNullOrEmpty(plainPassword);

        byte[] passwordBytes = [];
        byte[] derivedKey = [];

        try
        {
            // Dummy işleminde de gerçek parola doğrulamasında kullanılan
            // normalizasyon yöntemini uyguluyoruz.
            //
            // Böylece farklı karakter işleme davranışlarından kaynaklanan
            // gereksiz zamanlama farklılıkları azaltılır.
            if (!TryNormalizePassword(plainPassword, out passwordBytes)
                || passwordBytes.Length > MaxPasswordBytes)
            {
                // Başarısız normalizasyon veya uzunluk kontrolü sonucunda
                // oluşmuş olabilecek byte dizisini temizliyoruz.
                CryptographicOperations.ZeroMemory(passwordBytes);

                // Gerçek parola yerine sabit placeholder'ın bir kopyasını kullanıyoruz.
                // Böylece static placeholder yanlışlıkla temizlenmez.
                passwordBytes = (byte[])InvalidPasswordPlaceholder.Clone();
            }

            // Gerçek parola hash'lemesinde kullanılan PBKDF2 algoritmasını,
            // aynı iterasyon sayısını ve aynı çıktı uzunluğunu kullanıyoruz.
            //
            // Ancak burada gerçek bir hash üretmiyoruz.
            // Sonuç yalnızca zamanlama davranışını benzer hale getirmek için
            // hesaplanıyor ve hiçbir yerde saklanmıyor.
            //
            // DummySalt her çağrıda sabit kalır.
            derivedKey = Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes,
                DummySalt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            // derivedKey bilerek kullanılmıyor.
            // Amaç yalnızca PBKDF2 işlem maliyetini oluşturmaktır.
        }
        finally
        {
            // Kullanılan parola byte dizisini ve türetilmiş anahtarı temizliyoruz.
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    /// <summary>
    /// Veritabanında saklanan hash string değerini ayrıştırır ve doğrular.
    ///
    /// Desteklenen formatlar:
    ///
    /// V1:
    /// formatVersion$iterations$salt$hash
    ///
    /// V2:
    /// formatVersion$iterations$pepperVersion$salt$hash
    ///
    /// Bu metot yalnızca format ve parametre kontrollerini yapar.
    /// Parolanın doğruluğunu kontrol etmez.
    /// </summary>
    private static bool TryParseHash(
        string[] parts,
        out int formatVersion,
        out int iterations,
        out int pepperVersion,
        out byte[] salt,
        out byte[] expectedHash)
    {
        // out parametrelerinin varsayılan değerlerini belirliyoruz.
        //
        // Böylece herhangi bir kontrol başarısız olduğunda çağıran metot
        // tanımsız veya null byte dizileriyle karşılaşmaz.
        formatVersion = 0;
        iterations = 0;
        pepperVersion = 0;
        salt = [];
        expectedHash = [];

        try
        {
            // V2 formatı 5 parçadan oluşur:
            //
            // 2$600000$1$SALT$HASH
            //
            // İlk olarak format versiyonunu kontrol ediyoruz.
            if (parts.Length == 5)
            {
                // Format versiyonunun geçerli bir integer olmasını ve
                // desteklenen V2 formatına eşit olmasını bekliyoruz.
                //
                // Canonical integer kontrolü sayesinde "02" veya "+2"
                // gibi alternatif gösterimler de reddedilir.
                if (!TryParseCanonicalInt(parts[0], out formatVersion)
                    || formatVersion != CurrentFormatVersion)
                {
                    return false;
                }

                // İterasyon ve pepper versiyonu alanlarını parse ediyoruz.
                if (!TryParseCanonicalInt(parts[1], out iterations)
                    || !TryParseCanonicalInt(parts[2], out pepperVersion))
                {
                    return false;
                }

                // İterasyon sayısı sıfırdan büyük olmalıdır.
                // Ayrıca kaynak tüketimi saldırılarına karşı üst sınır uygulanır.
                if (iterations <= 0 || iterations > MaxAcceptedIterations)
                    return false;

                // Pepper versiyonu pozitif olmalıdır.
                if (pepperVersion <= 0)
                    return false;

                // Salt hexadecimal string'den byte dizisine dönüştürülür.
                salt = Convert.FromHexString(parts[3]);

                // Beklenen hash de hexadecimal string'den byte dizisine dönüştürülür.
                expectedHash = Convert.FromHexString(parts[4]);
            }
            // V1 formatı 4 parçadan oluşur:
            //
            // 1$600000$SALT$HASH
            //
            // Bu formatta pepper versiyonu bulunmaz.
            else if (parts.Length == 4)
            {
                // Yalnızca desteklenen eski V1 formatını kabul ediyoruz.
                if (!TryParseCanonicalInt(parts[0], out formatVersion)
                    || formatVersion != LegacyFormatVersion)
                {
                    return false;
                }

                // V1 hash'in kendi iterasyon sayısını okuyoruz.
                if (!TryParseCanonicalInt(parts[1], out iterations))
                    return false;

                // V1 formatında da iterasyon sınırını uyguluyoruz.
                if (iterations <= 0 || iterations > MaxAcceptedIterations)
                    return false;

                // V1 formatındaki salt ve hash alanlarını parse ediyoruz.
                salt = Convert.FromHexString(parts[2]);
                expectedHash = Convert.FromHexString(parts[3]);
            }
            else
            {
                // 4 veya 5 dışında parça sayısına sahip hash formatları desteklenmez.
                return false;
            }

            // Salt ve hash uzunluklarının beklenen değerlerde olduğunu kontrol ediyoruz.
            //
            // Salt: 16 byte
            // Hash: 32 byte
            //
            // Bu kontrol, eksik veya fazla uzunlukta hexadecimal değerlerin
            // geçerli hash olarak kabul edilmesini engeller.
            return salt.Length == SaltSize
                   && expectedHash.Length == HashSize;
        }
        catch (FormatException)
        {
            // Convert.FromHexString geçersiz hexadecimal veri aldığında
            // FormatException fırlatır.
            //
            // Örneğin:
            // - Geçersiz hexadecimal karakterler.
            // - Eksik hexadecimal byte gösterimi.
            //
            // Bu durumda uygulamanın hata vermesi yerine hash geçersiz
            // kabul edilerek false döndürülür.
            return false;
        }
    }

    /// <summary>
    /// Bir string değerin standart ve kanonik integer gösterimi olup olmadığını kontrol eder.
    ///
    /// Örneğin:
    /// "600000" -> kabul edilir.
    /// "0600000" -> reddedilir.
    /// "+600000" -> reddedilir.
    /// "-600000" -> reddedilir.
    /// " 600000" -> reddedilir.
    ///
    /// Böylece hash formatının tek ve standart bir gösterime sahip olması sağlanır.
    /// </summary>
    private static bool TryParseCanonicalInt(string value, out int result)
    {
        // NumberStyles.None kullanıldığı için:
        // - Başında veya sonunda boşluk kabul edilmez.
        // - Pozitif/negatif işaret kabul edilmez.
        // - Binlik veya ondalık ayırıcı kabul edilmez.
        //
        // InvariantCulture kullanarak sistemin bölgesel ayarlarından
        // bağımsız bir sayı parse işlemi gerçekleştiriyoruz.
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out result))
        {
            return false;
        }

        // Parse işlemi başarılı olsa bile orijinal string ile integer'ın
        // standart gösterimini karşılaştırıyoruz.
        //
        // Örneğin "0600000" int olarak 600000 değerine dönüşebilir.
        // Ancak standart gösterimi "600000" olduğu için reddedilir.
        return value == result.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parolayı standart Unicode FormC formatına normalize eder ve UTF-8 byte dizisine dönüştürür.
    ///
    /// Geçersiz UTF-16 verisi bulunursa false döndürür.
    ///
    /// Hash oluşturma ve doğrulama sırasında aynı normalizasyon yöntemi
    /// kullanılmalıdır. Aksi durumda aynı parola farklı byte dizileri
    /// üretebilir ve doğrulama başarısız olabilir.
    /// </summary>
    private static bool TryNormalizePassword(
        string password,
        out byte[] passwordBytes)
    {
        // Başlangıçta boş bir byte dizisi döndürüyoruz.
        passwordBytes = [];

        try
        {
            // Unicode FormC normalizasyonunu uyguluyoruz.
            //
            // Unicode karakterler farklı kod noktalarının birleşimiyle
            // veya tek bir kod noktasıyla temsil edilebilir.
            //
            // Örneğin görsel olarak aynı olan bazı karakter dizileri
            // bellekte farklı şekilde tutulabilir.
            //
            // FormC, mümkün olan birleşik karakter gösterimini tercih ederek
            // bu farklılıkları standart hale getirir.
            //
            // Bu normalizasyonun deployment sonrasında değiştirilmesi,
            // daha önce oluşturulmuş hash'lerin doğrulanmasını bozabilir.
            string normalizedPassword = password.Normalize(
                NormalizationForm.FormC);

            // Normalize edilmiş string'i strict UTF-8 ile byte dizisine dönüştürüyoruz.
            //
            // StrictUtf8, geçersiz UTF-16 karakterleri sessizce değiştirmez.
            // Bunun yerine hata oluşturur.
            passwordBytes = StrictUtf8.GetBytes(normalizedPassword);

            // Normalizasyon ve UTF-8 dönüşümü başarılı.
            return true;
        }
        catch (ArgumentException)
        {
            // Normalize veya UTF-8 dönüşümü sırasında geçersiz veri
            // nedeniyle ArgumentException oluşabilir.
            //
            // Bu durumda çağıran metoda hata fırlatmak yerine false döndürüyoruz.
            //
            // EncoderFallbackException da ArgumentException türevidir.
            return false;
        }
    }

    /// <summary>
    /// Verilen parola byte dizisiyle gerçek hash doğrulamasına benzer
    /// maliyette bir PBKDF2 işlemi gerçekleştirir.
    ///
    /// Üretilen anahtar hiçbir yerde kullanılmaz.
    ///
    /// Bu metot özellikle bozuk hash veya desteklenmeyen hash formatlarında
    /// zamanlama farklılıklarını azaltmak için kullanılır.
    /// </summary>
    private static void RunDummyDerivation(byte[] passwordBytes)
    {
        // PBKDF2 sonucunu saklamak için boş byte dizisi oluşturuyoruz.
        byte[] derivedKey = [];

        try
        {
            // Gerçek doğrulamada kullanılan PBKDF2 algoritmasını ve
            // iterasyon sayısını kullanarak maliyetli bir işlem gerçekleştiriyoruz.
            //
            // Ancak burada gerçek hash ile karşılaştırma yapılmaz.
            // İşlem yalnızca zamanlama davranışını benzer hale getirmek içindir.
            derivedKey = Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes,
                DummySalt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            // Üretilen anahtar bilerek kullanılmaz.
        }
        finally
        {
            // PBKDF2 sonucunu bellekte tutmamak için temizliyoruz.
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    /// <summary>
    /// Güncel pepper değerini döndürür.
    ///
    /// Geçerli pepper yapılandırması bulunamazsa uygulama hatası oluşturur.
    /// Çünkü güncel hash üretiminde pepper olmadan devam etmek güvenli değildir.
    /// </summary>
    private static byte[] GetCurrentPepper()
    {
        // Mevcut pepper versiyonuna ait anahtarı elde ediyoruz.
        //
        // null dönmesi durumunda bu bir uygulama yapılandırma hatasıdır.
        // Bu nedenle sessizce devam etmek yerine InvalidOperationException
        // fırlatıyoruz.
        return GetPepper(CurrentPepperVersion)
               ?? throw new InvalidOperationException(
                   "The current password pepper is invalid.");
    }

    /// <summary>
    /// İstenen pepper versiyonuna ait anahtarı döndürür.
    ///
    /// Şu an yalnızca CurrentPepperVersion desteklenmektedir.
    /// Desteklenmeyen versiyonlarda null döndürülür.
    ///
    /// Base64 formatındaki pepper byte dizisine dönüştürülür ve
    /// beklenen 32 byte uzunluğunda olup olmadığı kontrol edilir.
    /// </summary>
    private static byte[]? GetPepper(int version)
    {
        // İstenen versiyon mevcut versiyonla eşleşmiyorsa desteklenmiyor demektir.
        //
        // İleride pepper rotasyonu yapılırsa eski versiyonlar için
        // ayrı anahtar çözümleme mekanizması eklenebilir.
        if (version != CurrentPepperVersion)
            return null;

        byte[] pepper;

        try
        {
            // Base64 string'i byte dizisine dönüştürüyoruz.
            //
            // Base64 yalnızca binary veriyi string formatında temsil eder.
            // Şifreleme veya gizleme mekanizması değildir.
            pepper = Convert.FromBase64String(PepperBase64);
        }
        catch (FormatException exception)
        {
            // Pepper değeri geçerli Base64 formatında değilse bu bir
            // uygulama yapılandırma problemidir.
            //
            // FormatException yerine daha açıklayıcı bir
            // InvalidOperationException fırlatıyoruz.
            throw new InvalidOperationException(
                "The configured password pepper is not valid Base64.",
                exception);
        }

        // Pepper uzunluğunun tam olarak 32 byte olmasını bekliyoruz.
        //
        // HMAC-SHA256 farklı uzunluklardaki anahtarlarla çalışabilse de
        // burada uygulama tarafından belirlenen anahtar uzunluğu
        // yapılandırma standardı olarak kontrol edilmektedir.
        if (pepper.Length != 32)
        {
            // Uzunluk kontrolünden geçemeyen pepper byte dizisini temizliyoruz.
            CryptographicOperations.ZeroMemory(pepper);

            // Beklenen uzunluk sağlanmıyorsa güvenli şekilde devam etmiyoruz.
            throw new InvalidOperationException(
                "The configured password pepper must be exactly 32 bytes.");
        }

        // Kontroller başarılıysa pepper byte dizisini çağıran metoda döndürüyoruz.
        //
        // Çağıran metot, kullanım sonrasında bu diziyi ZeroMemory ile temizlemelidir.
        return pepper;
    }
}
