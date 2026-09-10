using System.Collections.Generic;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// One exhibit signboard, as the curriculum sheet describes the game: the biology concept it
    /// teaches, the science-literacy indicator a student should reach, and the virtual-game idea
    /// the sheet proposes. Keyed like <see cref="LessonSource"/>, so the same exhibit screen gets
    /// its lesson plaque and its signboard from one <c>GameKey</c>.
    /// </summary>
    internal struct SignageSource
    {
        public string GameKey;
        public int Number;
        public string DisplayName;
        public string Konsep;
        public string Indikator;
        public string Ide;
    }

    /// <summary>
    /// The eighteen signboards, transcribed from the curriculum spreadsheet
    /// <c>AMS-NAM.xlsx - V01</c> (columns "Konsep biologi utama", "Indikator literasi sains",
    /// "Ide game virtual"). Data, not code — a change here is a change to what the museum tells a
    /// visitor, and should come from the sheet.
    /// </summary>
    /// <remarks>
    /// Two rows are not verbatim. The sheet's Gobak Sodor indicator and game idea were a pasted
    /// copy of the Egrang row ("karakter egrang"), so they are rewritten in Gobak Sodor terms
    /// around the same concept the sheet gives it. The Bitingan indicator carried an author's
    /// working note ("Karena rincian mekaniknya … saya tempatkan sementara …") ahead of the
    /// indicator proper; only the indicator is kept.
    /// </remarks>
    internal static class GameSignageContent
    {
        public static IReadOnlyList<SignageSource> All() => Entries;

        public static bool TryGet(string gameKey, out SignageSource source)
        {
            foreach (SignageSource entry in Entries)
            {
                if (entry.GameKey == gameKey)
                {
                    source = entry;
                    return true;
                }
            }

            source = default;
            return false;
        }

        private static readonly SignageSource[] Entries =
        {
            Sign("dakon", 1, "Dakon",
                "Ekologi pertanian, pemanfaatan sumber daya hayati, alur hasil panen dalam sistem agroekosistem.",
                "Siswa mengidentifikasi hubungan manusia–tanaman–hasil panen; menjelaskan peran lumbung sebagai penyimpanan hasil hayati; menyimpulkan pentingnya pengelolaan sumber daya.",
                "Simulasi pengelolaan lahan dan panen: pemain memilih strategi distribusi biji/hasil panen agar “ekosistem ladang” tetap seimbang."),

            Sign("engklek", 2, "Engklek",
                "Sistem gerak, keseimbangan tubuh, pusat massa, koordinasi saraf–otot.",
                "Siswa menjelaskan mengapa tumpuan satu kaki menuntut keseimbangan; menghubungkan gerak dengan kerja otot dan sendi; menafsirkan perubahan postur saat melompat.",
                "Avatar harus menjaga keseimbangan saat berpindah petak; skor naik bila postur tubuh stabil dan tidak menyentuh garis."),

            Sign("cublak_cublak_suweng", 3, "Cublak-cublak Suweng",
                "Sistem pendengaran, transduksi rangsang bunyi, koordinasi sensorik.",
                "Siswa mengidentifikasi peran bunyi sebagai rangsang; menjelaskan bagaimana suara diproses oleh sistem saraf; menafsirkan petunjuk auditif dalam permainan.",
                "Mini-game menebak lokasi benda berdasarkan petunjuk suara; pemain belajar hubungan bunyi–respons."),

            Sign("egrang", 4, "Egrang",
                "Sistem gerak manusia, koordinasi otot dan saraf, keseimbangan tubuh, fungsi otot, fungsi sendi, sistem pernapasan, kelelahan otot.",
                "Siswa menjelaskan hubungan pijakan, pusat berat badan, dan kestabilan; menafsirkan mengapa tubuh mudah jatuh jika posisi tidak tepat; membandingkan tinggi pijakan dengan kestabilan.",
                "Simulasi berjalan di atas egrang dengan pengaturan titik tumpu; pemain mengatur langkah agar tidak jatuh."),

            Sign("gobak_sodor", 5, "Gobak Sodor",
                "Kebugaran jasmani, daya tahan, koordinasi gerak cepat, respons motorik.",
                "Siswa menjelaskan peran tulang, otot, sendi, dan rangka saat tubuh berlari dan berbelok cepat; menghubungkan gerak menghadang dan meloloskan diri dengan kerja sistem saraf; menganalisis mengapa daya tahan menentukan lamanya bermain.",
                "Arena bergaris: pemain menerobos penjagaan dengan tombol arah dan ritme lari. Indikator “respon saraf” menuntut tekanan tombol tepat waktu untuk mengelak; bar stamina turun bila berlari terus."),

            Sign("bentengan", 6, "Bentengan",
                "Kecepatan, kelincahan, koordinasi saraf–otot, kebugaran kardiorespirasi.",
                "Siswa menjelaskan mengapa mengejar dan menghindar memerlukan reaksi cepat; menafsirkan perubahan napas dan denyut jantung; menghubungkan kerja tim dengan efektivitas gerak.",
                "Game berbasis zona aman: pemain harus bergerak cepat, memilih jalur aman, dan menyelamatkan “anggota tim”."),

            Sign("benthik", 7, "Benthik",
                "Motorik halus, koordinasi mata-tangan, kontrol gerak, refleks.",
                "Siswa mengamati keterampilan mengayun dan menangkap; menjelaskan hubungan visual–motorik; menarik kesimpulan tentang pentingnya ketepatan gerak.",
                "Pemain mengatur sudut pukulan dan waktu tangkap untuk melontarkan benda sejauh mungkin."),

            Sign("gatheng", 8, "Gatheng",
                "Motorik halus, koordinasi visual, fokus perhatian, akurasi gerak.",
                "Siswa mengidentifikasi ketepatan pengambilan dan lemparan benda; membandingkan gerak yang berhasil dan gagal; menyimpulkan peran latihan koordinasi.",
                "Game ketangkasan berbasis urutan ambil-lempar-tangkap dengan tingkat kesulitan bertahap."),

            Sign("bekelan", 9, "Bekelan",
                "Koordinasi tangan-mata, motorik halus, kontrol gerak berulang.",
                "Siswa menjelaskan bagaimana gerak berurutan memerlukan ketelitian; mengamati pola salah/benar; menafsirkan pentingnya konsentrasi dan ritme gerak.",
                "Simulasi level bertahap: lempar bola, ambil biji, ubah posisi, lalu lanjut ke tahap berikutnya."),

            Sign("lompat_tali", 10, "Lompat Tali",
                "Kebugaran jantung-paru, kekuatan otot, daya tahan, koordinasi ritme.",
                "Siswa menjelaskan mengapa aktivitas lompat meningkatkan kerja jantung dan paru; menghubungkan latihan berulang dengan daya tahan; menyimpulkan manfaat aktivitas fisik.",
                "Level ketinggian bertahap: pemain melompati tali yang semakin tinggi sambil menjaga ritme dan stamina."),

            Sign("dam_daman", 11, "Dam-daman",
                "Biologi kognitif/perilaku: perhatian, pengambilan keputusan, perencanaan gerak.",
                "Siswa mengamati pola, memprediksi langkah, dan menilai risiko; menyimpulkan bahwa strategi dipengaruhi oleh proses berpikir; membedakan keputusan tepat dan keliru.",
                "Papan strategi dengan misi “jaga pion dan baca pola lawan”; cocok untuk mode kolaboratif dan kompetitif."),

            Sign("cirak", 12, "Cirak",
                "Motorik halus, koordinasi visual, ketepatan arah gerak.",
                "Siswa mengaitkan arah lemparan/tembakan dengan keberhasilan mengenai sasaran; mengamati hubungan kekuatan–arah–hasil; menyimpulkan pentingnya latihan koordinasi.",
                "Game kelereng virtual dengan sasaran, lintasan, dan hambatan untuk melatih presisi."),

            Sign("dampar", 13, "Dampar",
                "Biologi kognitif/perilaku: strategi, perhatian, pemecahan masalah, kontrol diri.",
                "Siswa mengamati aturan dan pola, lalu menyusun strategi; menjelaskan bahwa keputusan dipengaruhi informasi visual dan pengalaman; menyimpulkan hubungan otak–tindakan.",
                "Simulasi papan strategi berbasis giliran dengan tantangan memilih langkah terbaik."),

            Sign("sluku_sluku_bathok", 14, "Sluku-sluku Bathok",
                "Pendengaran, bahasa, ritme, respons sosial.",
                "Siswa mengenali pola bunyi dan bahasa; menjelaskan keterkaitan irama dengan respons kelompok; menafsirkan peran vokal dalam kerja sama.",
                "Mode “ikuti irama”: pemain menekan tombol sesuai ketukan dan lirik yang muncul."),

            Sign("jamuran", 15, "Jamuran",
                "Gerak tubuh, koordinasi kelompok, kebugaran ringan, respons motorik.",
                "Siswa menjelaskan hubungan gerak berkelompok dengan koordinasi tubuh; mengamati sinkronisasi langkah; menyimpulkan pentingnya kerja sama dalam aktivitas fisik.",
                "Arena lingkaran interaktif dengan gerakan serempak; skor berdasarkan sinkronisasi tim."),

            Sign("bitingan", 16, "Bitingan",
                "Motorik halus dan koordinasi gerak, dengan penekanan pada ketepatan aksi.",
                "Siswa mengamati, meniru, dan mengevaluasi ketepatan gerak; membandingkan percobaan yang berhasil dan gagal; menyimpulkan peran latihan bagi koordinasi motorik halus.",
                "Mini-game “tepat sasaran” dengan aksi sederhana dan target kecil; level bertahap."),

            Sign("ancak_ancak_alis", 17, "Ancak-ancak Alis",
                "Ekologi pertanian, tanaman, hama, musim, interaksi makhluk hidup dan lingkungan.",
                "Siswa mengidentifikasi organisme yang menguntungkan dan mengganggu tanaman; menjelaskan pengaruh musim terhadap pertanian; menyimpulkan pentingnya observasi lingkungan.",
                "Simulasi sawah-desa: pemain memantau kondisi tanaman, hama, dan musim lalu mengambil keputusan."),

            Sign("blarak_sempal", 18, "Blarak Sempal",
                "Aktivitas fisik kelompok, koordinasi otot, keseimbangan, kebugaran ringan.",
                "Siswa menafsirkan bagaimana gerak bersama memerlukan koordinasi dan kekuatan otot; mengamati perubahan posisi tubuh; menyimpulkan manfaat gerak berkelompok bagi kebugaran.",
                "Game tarik-susun posisi tubuh dalam kelompok dengan tantangan sinkronisasi dan keseimbangan."),
        };

        private static SignageSource Sign(string key, int number, string name, string konsep, string indikator, string ide) =>
            new SignageSource
            {
                GameKey = key,
                Number = number,
                DisplayName = name,
                Konsep = konsep,
                Indikator = indikator,
                Ide = ide,
            };
    }
}
