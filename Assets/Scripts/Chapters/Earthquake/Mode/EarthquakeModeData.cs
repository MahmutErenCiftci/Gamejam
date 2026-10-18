using System;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    // Assets/Data/Earthquake/earthquake_mode.json dosyasının yapısı (JsonUtility ile okunur).
    // Alan adları JSON anahtarlarıyla birebir aynıdır. JSON'da olmayan alanlar CreateDefault() değerleriyle doldurulur,
    // böylece veri eksik olsa da akış kilitlenmez.

    /// <summary>Deprem modunun bütün metin/ayar verisi (EQ_SPEC şeması + STORY'nin ek alanları).</summary>
    [Serializable]
    public class EarthquakeModeData
    {
        public string version = "1.0";
        public float countdownSeconds = 60f;
        public EqPhoneData phone;
        public EqInspectableData[] inspectables;
        public EqWarningData[] warnings;
        public QuestionData[] questions;
        public EqProtectionTexts protection;
        public EqAfterTexts after;
        public EqResultTexts results;

        /// <summary>id'ye göre inceleme verisi (büyük/küçük harf, "_" ve "-" yok sayılır).</summary>
        public EqInspectableData FindInspectable(string id) =>
            inspectables == null ? null : Array.Find(inspectables, x => x != null && SameId(x.id, id));

        public EqWarningData FindWarning(string id) =>
            warnings == null ? null : Array.Find(warnings, x => x != null && SameId(x.id, id));

        public QuestionData FindQuestion(string id) =>
            questions == null ? null : Array.Find(questions, x => x != null && SameId(x.id, id));

        /// <summary>"Bedroom_Window", "bedroomwindow", "bedroom-window" aynı kabul edilir.</summary>
        public static bool SameId(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && NormalizeId(a) == NormalizeId(b);

        public static string NormalizeId(string id) =>
            string.IsNullOrEmpty(id) ? "" : id.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();

        /// <summary>JSON'u okur, eksik bölümleri varsayılanlarla doldurur. json null/bozuksa tamamen varsayılan döner.</summary>
        public static EarthquakeModeData Load(TextAsset json)
        {
            var defaults = CreateDefault();
            if (json == null || string.IsNullOrWhiteSpace(json.text)) return defaults;

            EarthquakeModeData data;
            try
            {
                data = JsonUtility.FromJson<EarthquakeModeData>(json.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EQ] earthquake_mode.json okunamadı, varsayılan metinler kullanılıyor: {e.Message}");
                return defaults;
            }
            if (data == null) return defaults;
            data.MergeDefaults(defaults);
            return data;
        }

        private void MergeDefaults(EarthquakeModeData d)
        {
            if (countdownSeconds <= 0f) countdownSeconds = d.countdownSeconds;
            if (phone == null) phone = d.phone;
            else
            {
                phone.app = Or(phone.app, d.phone.app);
                phone.title = Or(phone.title, d.phone.title);
                phone.body = Or(phone.body, d.phone.body);
                phone.note = Or(phone.note, d.phone.note);
                phone.button = Or(phone.button, d.phone.button);
            }
            if (inspectables == null || inspectables.Length == 0) inspectables = d.inspectables;
            if (warnings == null || warnings.Length == 0) warnings = d.warnings;
            if (questions == null || questions.Length == 0) questions = d.questions;
            foreach (var q in questions)
            {
                if (q == null) continue;
                if (q.timeLimit <= 0f) q.timeLimit = 5f;
                if (q.penalty < 0) q.penalty = 30;
                if (string.IsNullOrEmpty(q.speaker)) q.speaker = "ANNE";
                var dq = d.FindQuestion(q.id);
                if (dq != null)
                {
                    q.topic = Or(q.topic, dq.topic);
                    q.lesson = Or(q.lesson, dq.lesson);
                }
            }

            if (protection == null) protection = d.protection;
            else
            {
                var p = d.protection;
                if (protection.steps == null || protection.steps.Length == 0) protection.steps = p.steps;
                protection.promptDrop = Or(protection.promptDrop, p.promptDrop);
                protection.promptCover = Or(protection.promptCover, p.promptCover);
                protection.promptHold = Or(protection.promptHold, p.promptHold);
                protection.promptDropInPlace = Or(protection.promptDropInPlace, p.promptDropInPlace);
                protection.promptCoverInPlace = Or(protection.promptCoverInPlace, p.promptCoverInPlace);
                protection.releaseWarning = Or(protection.releaseWarning, p.releaseWarning);
                protection.noResponseHint = Or(protection.noResponseHint, p.noResponseHint);
                protection.tableSuccess = Or(protection.tableSuccess, p.tableSuccess);
                protection.inPlaceSuccess = Or(protection.inPlaceSuccess, p.inPlaceSuccess);
                protection.none = Or(protection.none, p.none);
                if (protection.exposed == null) protection.exposed = p.exposed;
                else
                {
                    protection.exposed.window = Or(protection.exposed.window, p.exposed.window);
                    protection.exposed.mirror = Or(protection.exposed.mirror, p.exposed.mirror);
                    protection.exposed.bookshelf = Or(protection.exposed.bookshelf, p.exposed.bookshelf);
                }
            }

            if (after == null) after = d.after;
            else
            {
                after.checkHazards = Or(after.checkHazards, d.after.checkHazards);
                after.glassWarning = Or(after.glassWarning, d.after.glassWarning);
                after.elevator = Or(after.elevator, d.after.elevator);
                after.stairs = Or(after.stairs, d.after.stairs);
                after.bagTaken = Or(after.bagTaken, d.after.bagTaken);
                after.bagReminder = Or(after.bagReminder, d.after.bagReminder);
                after.doorLockedBefore = Or(after.doorLockedBefore, d.after.doorLockedBefore);
                after.assembly = Or(after.assembly, d.after.assembly);
                after.readyHint = Or(after.readyHint, d.after.readyHint);
            }

            if (results == null) results = d.results;
            else
            {
                results.title = Or(results.title, d.results.title);
                results.readyText = Or(results.readyText, d.results.readyText);
                results.note = Or(results.note, d.results.note);
                if (results.labels == null || results.labels.Length == 0) results.labels = d.results.labels;
                if (results.labelMessages == null) results.labelMessages = d.results.labelMessages;
                if (results.lessons == null) results.lessons = d.results.lessons;
                else results.lessons.MergeDefaults(d.results.lessons);
                if (results.tips == null) results.tips = d.results.tips;
                else
                {
                    var t = d.results.tips;
                    results.tips.wrongAnswer = Or(results.tips.wrongAnswer, t.wrongAnswer);
                    results.tips.timeout = Or(results.tips.timeout, t.timeout);
                    results.tips.noProtection = Or(results.tips.noProtection, t.noProtection);
                    results.tips.exposure = Or(results.tips.exposure, t.exposure);
                    results.tips.elevator = Or(results.tips.elevator, t.elevator);
                    results.tips.noBag = Or(results.tips.noBag, t.noBag);
                    results.tips.fewHazards = Or(results.tips.fewHazards, t.fewHazards);
                }
            }
        }

        private static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

        /// <summary>JSON yokken de oynanabilmesi için gömülü varsayılan metinler (soru metinleri EQ_SPEC'teki gibi birebir).</summary>
        public static EarthquakeModeData CreateDefault()
        {
            return new EarthquakeModeData
            {
                version = "default",
                countdownSeconds = 60f,
                phone = new EqPhoneData
                {
                    app = "EŞİK",
                    title = "DEPREM HAZIRLIĞI: 01:00",
                    body = "Bir dakikan var. Evi dolaş, tehlikeleri bul, hazırlan.",
                    note = "",
                    button = "BAŞLA"
                },
                inspectables = Array.Empty<EqInspectableData>(),
                warnings = new[]
                {
                    new EqWarningData { id = "mirror", title = "CAM TEHLİKESİ!", text = "Deprem sırasında cam ve kırılabilecek eşyalardan uzak dur." },
                    new EqWarningData { id = "window", title = "PENCERE TEHLİKESİ!", text = "Camlar kırılabilir. Deprem anında pencerenin önünde durma." },
                    new EqWarningData { id = "bookshelf", title = "DEVRİLME TEHLİKESİ!", text = "Sabitlenmemiş uzun eşyalar devrilebilir. Yanında bekleme." },
                    new EqWarningData { id = "glass_after", title = "CAM KIRIKLARI!", text = "Yerde cam kırıkları var. Üstünden geçme!" }
                },
                questions = new[]
                {
                    Q("Q1", "Deprem sırasında ne yapmalıyız?", "Evet! Sağlam masanın altında Çök–Kapan–Tutun.", "protect",
                        "Deprem anı: sağlam masanın altında Çök–Kapan–Tutun.",
                        O("Pencereye koşmalıyız", false, "Camlar kırılabilir. Pencerelerden uzak durmalıyız."),
                        O("Sağlam bir masanın altında Çök–Kapan–Tutun yapmalıyız", true, "Masa, düşebilecek eşyalardan korur."),
                        O("Asansöre binmeliyiz", false, "Depremde asansör kullanılmaz."),
                        O("Merdivene koşmalıyız", false, "Sarsıntıda koşarken düşebiliriz.")),
                    Q("Q2", "Deprem sırasında asansörü kullanmak doğru mu?", "Doğru! Asansör kullanılmaz.", "elevator",
                        "Asansör: kullanılmaz, merdiveni kullan.",
                        O("Evet, daha hızlı ineriz", false, "Elektrik kesilebilir, mahsur kalırız."),
                        O("Hayır, asansörden uzak durmalıyız", true, "Depremde ve sonrasında merdiven kullanılır."),
                        O("Sadece elektrik varsa kullanabiliriz", false, "Elektrik her an kesilebilir.")),
                    Q("Q3", "Deprem sırasında camlara yakın durmalı mıyız?", "Evet. Camlardan uzak dururuz.", "glass",
                        "Camlar: pencere ve aynalardan uzak dur.",
                        O("Evet", false, "Camlar kırılıp zarar verebilir."),
                        O("Hayır, camlardan uzak durmalıyız", true, "Kırılan cam parçaları yaralayabilir."),
                        O("Pencereden dışarı bakmalıyız", false, "Pencere önü deprem anında tehlikelidir.")),
                    Q("Q4", "Sarsıntı bittikten sonra ilk ne yapmalıyız?", "Doğru! Önce tehlikeleri kontrol ederiz.", "after",
                        "Sarsıntıdan sonra: önce çevredeki tehlikeleri kontrol et.",
                        O("Çevremizdeki tehlikeleri kontrol etmeliyiz", true, "Cam kırıkları ve gaz kokusu kontrol edilir."),
                        O("Hemen asansöre binmeliyiz", false, "Depremden sonra da asansör kullanılmaz."),
                        O("Hasarlı binaya geri dönmeliyiz", false, "Hasarlı binaya yetkililer izin vermeden girilmez."))
                },
                protection = new EqProtectionTexts
                {
                    steps = new[] { "ÇÖK", "KAPAN", "TUTUN" },
                    promptDrop = "[E] Çök",
                    promptCover = "[E] Başını ve enseni koru",
                    promptHold = "[BOŞLUK] basılı tut, [A] / [D] ile dengede kal",
                    promptDropInPlace = "[E] Olduğun yerde çök",
                    promptCoverInPlace = "[E] Kollarınla başını ve enseni koru",
                    releaseWarning = "Sarsıntı bitene kadar tutun!",
                    noResponseHint = "Hemen korun! [E] ile çök, başını ve enseni koru.",
                    tableSuccess = "Sağlam masanın altında Çök–Kapan–Tutun yaptın.",
                    inPlaceSuccess = "Olduğun yerde çöktün, başını ve enseni korudun.",
                    exposed = new EqExposedTexts
                    {
                        window = "Pencereye çok yakındın! Deprem anında pencereden uzak dur.",
                        mirror = "Aynaya çok yakındın! Kırılabilecek camlardan uzak durmalısın.",
                        bookshelf = "Kitaplığın yanındaydın! Devrilebilecek eşyalardan uzak durmalısın."
                    },
                    none = "Korunma yapılmadı. Sarsıntı başlayınca hemen Çök–Kapan–Tutun yapmalısın."
                },
                after = new EqAfterTexts
                {
                    checkHazards = "Önce çevreni kontrol et: cam kırıkları, düşen eşyalar, gaz kokusu.",
                    glassWarning = "Yerde cam kırıkları var. Üstünden geçme!",
                    elevator = "Asansör kullanılmaz! Merdiveni kullan.",
                    stairs = "Merdivenden koşmadan, tırabzana tutunarak in.",
                    bagTaken = "Deprem çantanı aldın.",
                    bagReminder = "Çıkmadan önce kapının yanındaki deprem çantanı al.",
                    doorLockedBefore = "Sarsıntı sırasında dışarı koşulmaz. Şimdi evde güvenli yerini belirle.",
                    assembly = "Toplanma alanına ulaştın. Resmî duyuruları takip et.",
                    readyHint = "Güvenli bir yerde bekle. [BOŞLUK] Zamanı hızlandır"
                },
                results = new EqResultTexts
                {
                    title = "DEPREM FARKINDALIK RAPORU",
                    labels = new[]
                    {
                        new EqLabel { minScore = 85, label = "AFET BİLİNCİ USTASI" },
                        new EqLabel { minScore = 65, label = "HAZIRLIKLI" },
                        new EqLabel { minScore = 40, label = "GELİŞİYOR" },
                        new EqLabel { minScore = 0, label = "DAHA FAZLA ÖĞREN" }
                    },
                    labelMessages = Array.Empty<EqLabelMessage>(),
                    readyText = "Hazırlık tamam",
                    note = "",
                    lessons = EqLessons.CreateDefault(),
                    tips = new EqTips
                    {
                        wrongAnswer = "Yanlış cevapların açıklamalarını tekrar oku.",
                        timeout = "Zor anda da güvenli davranışı hatırla: Çök–Kapan–Tutun.",
                        noProtection = "Sarsıntı başlayınca hemen Çök–Kapan–Tutun.",
                        exposure = "Deprem anında camlardan ve devrilebilecek eşyalardan uzak dur.",
                        elevator = "Depremde ve sonrasında asansör kullanılmaz.",
                        noBag = "Sarsıntı bitince deprem çantanı al.",
                        fewHazards = "Bir dahaki sefere evdeki daha çok eşyayı incele."
                    }
                }
            };
        }

        private static QuestionData Q(string id, string question, string correctFeedback, string topic, string lesson, params QuestionOption[] options) =>
            new QuestionData { id = id, speaker = "ANNE", question = question, timeLimit = 5f, penalty = 30, correctFeedback = correctFeedback,
                               topic = topic, lesson = lesson, options = options };

        private static QuestionOption O(string text, bool correct, string explanation) =>
            new QuestionOption { text = text, correct = correct, explanation = explanation };
    }

    /// <summary>Telefon bildirimi metinleri.</summary>
    [Serializable]
    public class EqPhoneData
    {
        public string app, title, body, note, button;
    }

    /// <summary>İncelenebilir nesnenin kart metni. hazard: "Info" / "Hazard" / "Protective" / "Safety" / "Exit".</summary>
    [Serializable]
    public class EqInspectableData
    {
        public string id, title, text, hazard;
        /// <summary>Sarsıntı sonrası kart metni (boşsa text kullanılır).</summary>
        public string afterText;

        /// <summary>hazard yazısını enum'a çevirir; tanınmazsa fallback döner.</summary>
        public HazardClass GetHazardClass(HazardClass fallback)
        {
            if (!string.IsNullOrEmpty(hazard) && Enum.TryParse(hazard, true, out HazardClass value)) return value;
            return fallback;
        }
    }

    /// <summary>Tehlike bölgesi uyarı toast'ı (mirror, window, bookshelf, glass_after).</summary>
    [Serializable]
    public class EqWarningData
    {
        public string id, title, text;
    }

    /// <summary>Annenin süreli sorusu.</summary>
    [Serializable]
    public class QuestionData
    {
        public string id;
        public string speaker;
        public string question;
        public float timeLimit = 5f;
        public int penalty = 30;
        public string correctFeedback;
        /// <summary>Süre dolunca gösterilecek doğru cevap açıklaması.</summary>
        public string timeoutFeedback;
        /// <summary>Rapordaki "Öğrenilecekler" konusu (aynı konudaki dersler tek madde olur): protect, glass, elevator, after...</summary>
        public string topic;
        /// <summary>Rapordaki kısa ders maddesi ("Konu: doğru davranış"). Boşsa doğru şık kullanılır.</summary>
        public string lesson;
        public QuestionOption[] options;

        /// <summary>İlk doğru şıkkın sırası (yoksa -1).</summary>
        public int CorrectIndex => options == null ? -1 : Array.FindIndex(options, o => o != null && o.correct);

        /// <summary>İlk yanlış şıkkın sırası (yoksa -1).</summary>
        public int FirstWrongIndex => options == null ? -1 : Array.FindIndex(options, o => o != null && !o.correct);

        public string[] OptionTexts()
        {
            if (options == null) return Array.Empty<string>();
            var texts = new string[options.Length];
            for (int i = 0; i < options.Length; i++) texts[i] = options[i] != null ? options[i].text : "";
            return texts;
        }
    }

    [Serializable]
    public class QuestionOption
    {
        public string text;
        public bool correct;
        public string explanation;
    }

    /// <summary>Çök–Kapan–Tutun metinleri.</summary>
    [Serializable]
    public class EqProtectionTexts
    {
        public EqBalanceTexts balance = new EqBalanceTexts();
        public string[] steps;
        public string promptDrop, promptCover, promptHold;
        public string promptDropInPlace, promptCoverInPlace;
        public string releaseWarning, noResponseHint, tableSuccess, inPlaceSuccess;
        public EqExposedTexts exposed;
        public string none;

        /// <summary>Maruziyet türüne ("window"/"mirror"/"bookshelf") göre metin.</summary>
        public string ExposedText(string kind)
        {
            if (exposed == null) return null;
            switch (EarthquakeModeData.NormalizeId(kind))
            {
                case "window": return exposed.window;
                case "mirror": return exposed.mirror;
                case "bookshelf": return exposed.bookshelf;
                default: return exposed.window;
            }
        }
    }

    [Serializable]
    public class EqBalanceTexts
    {
        // Denge göstergesi (EarthquakeBalanceHUD): başlık / alt başlık, talimat, durum satırı, SPACE şeridi, sonuç.
        public string warning = "DEPREM!";
        public string title = "TUTUN!";
        public string subtitle = "Sarsıntı bitene kadar tutun ve dengede kal";
        /// <summary>Talimat (denge başlamadan): durum satırı.</summary>
        public string controls = "İşareti YEŞİLDE tut:  A sola,  D sağa çeker";
        /// <summary>Talimat (denge başlamadan): SPACE şeridi.</summary>
        public string startHint = "BOŞLUK tuşuna bas ve bırakma";
        /// <summary>SPACE basılıyken şerit metni.</summary>
        public string note = "Tutunuyorsun. Başını ve enseni koru!";
        /// <summary>SPACE bırakılınca şerit metni.</summary>
        public string release = "Bırakma, tutunmaya devam et!";
        public string initial = "Sarsıntı başladı";
        public string strong = "Sarsıntı güçleniyor";
        public string irregular = "Sarsıntı düzensiz, dikkat!";
        public string stable = "Dengedesin, böyle devam!";
        public string unstable = "Dengeni koru!";
        public string driftLeft = "A ile ortaya dön";
        public string driftRight = "D ile ortaya dön";
        public string timeLeft = "{0} sn";
        public string resultGood = "Sarsıntı bitti. Sıkı tutundun!";
        public string resultHard = "Sarsıntı bitti. Biraz zorlandın ama korunmaya devam ettin.";
        // Sarsıntı sonrası kart / rapor
        public string success = "Sarsıntı bitene kadar tutundun ve dengeni korudun.";
        public string feedback = "Biraz zorlandın. Sarsıntı bitene kadar BOŞLUK tuşunu bırakma.";
        public string score = "Denge: {0}/100";
        public string resultNote = "Sarsıntı bitene kadar başını ve enseni korumaya devam et.";
    }

    [Serializable]
    public class EqExposedTexts
    {
        public string window, mirror, bookshelf;
    }

    /// <summary>Sarsıntı sonrası metinleri.</summary>
    [Serializable]
    public class EqAfterTexts
    {
        public string checkHazards, glassWarning, elevator, stairs, bagTaken, bagReminder, doorLockedBefore, assembly, readyHint;
    }

    /// <summary>Rapor metinleri.</summary>
    [Serializable]
    public class EqResultTexts
    {
        public string title;
        public EqLabel[] labels;
        public EqLabelMessage[] labelMessages;
        public string readyText;
        /// <summary>Rapordaki kısa "Öğrenilecekler" maddeleri (konu başına bir madde).</summary>
        public EqLessons lessons;
        public EqTips tips;
        public string note;

        /// <summary>Puana göre etiket (minScore'u en yüksek olan uygun etiket).</summary>
        public string LabelFor(int score)
        {
            string best = null;
            int bestMin = int.MinValue;
            if (labels != null)
                foreach (var l in labels)
                    if (l != null && score >= l.minScore && l.minScore > bestMin) { best = l.label; bestMin = l.minScore; }
            return best ?? "";
        }

        /// <summary>Puana göre kısa mesaj (labelMessages; yoksa boş).</summary>
        public string MessageFor(int score)
        {
            string best = null;
            int bestMin = int.MinValue;
            if (labelMessages != null)
                foreach (var l in labelMessages)
                    if (l != null && score >= l.minScore && l.minScore > bestMin) { best = l.text; bestMin = l.minScore; }
            return best ?? "";
        }
    }

    [Serializable]
    public class EqLabel
    {
        public int minScore;
        public string label;
    }

    [Serializable]
    public class EqLabelMessage
    {
        public int minScore;
        public string text;
    }

    /// <summary>
    /// Rapordaki kısa ders maddeleri. Her alan bir konu anahtarıdır (QuestionData.topic ile aynı adlar);
    /// aynı konuya düşen yanlışlar raporda tek madde olarak görünür.
    /// </summary>
    [Serializable]
    public class EqLessons
    {
        public string protect, hold, glass, furniture, elevator, after, glassAfter, bag, hazards;

        /// <summary>Konu anahtarına göre madde (bilinmeyen konu = null).</summary>
        public string For(string topic)
        {
            switch (EarthquakeModeData.NormalizeId(topic))
            {
                case "protect": return protect;
                case "hold": return hold;
                case "glass": return glass;
                case "furniture": return furniture;
                case "elevator": return elevator;
                case "after": return after;
                case "glassafter": return glassAfter;
                case "bag": return bag;
                case "hazards": return hazards;
                default: return null;
            }
        }

        public void MergeDefaults(EqLessons d)
        {
            if (d == null) return;
            protect = string.IsNullOrEmpty(protect) ? d.protect : protect;
            hold = string.IsNullOrEmpty(hold) ? d.hold : hold;
            glass = string.IsNullOrEmpty(glass) ? d.glass : glass;
            furniture = string.IsNullOrEmpty(furniture) ? d.furniture : furniture;
            elevator = string.IsNullOrEmpty(elevator) ? d.elevator : elevator;
            after = string.IsNullOrEmpty(after) ? d.after : after;
            glassAfter = string.IsNullOrEmpty(glassAfter) ? d.glassAfter : glassAfter;
            bag = string.IsNullOrEmpty(bag) ? d.bag : bag;
            hazards = string.IsNullOrEmpty(hazards) ? d.hazards : hazards;
        }

        public static EqLessons CreateDefault() => new EqLessons
        {
            protect = "Deprem anı: sağlam masanın altında Çök–Kapan–Tutun.",
            hold = "Tutun: sarsıntı bitene kadar masanın ayağını bırakma.",
            glass = "Camlar: pencere ve aynalardan uzak dur.",
            furniture = "Eşyalar: devrilebilecek dolapların yanında durma.",
            elevator = "Asansör: kullanılmaz, merdiveni kullan.",
            after = "Sarsıntıdan sonra: önce çevredeki tehlikeleri kontrol et.",
            glassAfter = "Cam kırıkları: üstünden geçme, etrafından dolaş.",
            bag = "Deprem çantası: sarsıntı bitince al, sonra çık.",
            hazards = "Hazırlık: evdeki riskli eşyaları önceden incele."
        };
    }

    [Serializable]
    public class EqTips
    {
        public string wrongAnswer, timeout, noProtection, exposure, elevator, noBag, fewHazards;
    }
}
