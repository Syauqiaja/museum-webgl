namespace Museum.Core.EditorTools
{
    /// <summary>One lobby station's plaque: a title and its tabbed sections.</summary>
    internal struct LobbyLesson
    {
        /// <summary>Resources key, <c>lobby_&lt;name&gt;</c>, so it can never collide with a game's.</summary>
        public string Key;
        public string Title;
        public (string heading, string body)[] Sections;
    }

    /// <summary>
    /// The ground-floor gallery's copy: not one game's science, which the eighteen bays above
    /// already carry, but the culture the games come out of — what a dolanan is, the songs, the
    /// values, how a round is opened, and what the visitor can set off in this room. Written
    /// for the plaque, so each section is a few short paragraphs a visitor reads standing up.
    /// </summary>
    /// <remarks>
    /// Editor-side authoring source only, like <see cref="LessonContent"/>:
    /// MuseumLobbyGalleryBuilder writes it into GameLessonData assets under
    /// Assets/Resources/lessons/lobby_*.asset and the runtime reads those.
    /// </remarks>
    internal static class LobbyLessonContent
    {
        public static LobbyLesson Sambutan => new LobbyLesson
        {
            Key = "lobby_sambutan",
            Title = "Dolanan Anak Jawa",
            Sections = new[]
            {
                ("SAMBUTAN",
                 "Selamat datang di Museum Permainan Tradisional. Lantai ini adalah pengantar: tentang "
                 + "apa itu dolanan anak, dari mana ia lahir, lagu yang mengiringinya, dan nilai yang "
                 + "diam-diam diajarkannya.\n\nDelapan belas permainan menunggu di lantai atas — masing-masing "
                 + "dengan tayangan, papan pelajaran, dan tiga di antaranya dapat dimainkan bersama teman "
                 + "secara daring: Dakon, Engklek, dan Egrang."),
                ("APA ITU DOLANAN",
                 "Dalam bahasa Jawa, dolan berarti bermain atau bepergian untuk bersenang-senang; dolanan "
                 + "adalah kegiatannya sekaligus alatnya. Dolanan anak tumbuh di halaman rumah, di "
                 + "pelataran, di tanah lapang sehabis panen, dan di bawah terang bulan.\n\nHampir semuanya "
                 + "tidak membutuhkan alat yang dibeli. Biji sawo, pecahan genting, batu kali, bambu, "
                 + "pelepah kelapa, dan tali dari karet gelang sudah cukup. Yang dibutuhkan hanyalah "
                 + "teman — dan itulah inti dolanan: permainan ini hampir tidak pernah dimainkan sendirian."),
                ("CARA MENJELAJAH",
                 "Berjalanlah dengan WASD dan lihat sekeliling dengan tetikus (atau stik virtual di layar "
                 + "sentuh). Di dekat papan seperti ini, tombol Q dan E membalik halaman.\n\nBeberapa "
                 + "benda di lantai ini bisa disentuh: tabuhlah gong, putar gasing raksasa, injak petak "
                 + "engklek berurutan, dan dengarkan tembang dolanan di panggung. Dekati bendanya dan "
                 + "tekan Enter (atau ketuk Interaksi).\n\nTangga di sebelah barat membawa Anda ke ruang "
                 + "pamer di lantai atas."),
                ("18 PERMAINAN",
                 "Ruang pamer di atas menampilkan Dakon, Engklek, Cublak-cublak Suweng, Egrang, Gobak Sodor, "
                 + "Bentengan, Benthik, Gatheng, Bekelan, Lompat Tali, Dam-daman, Cirak, Dampar, "
                 + "Sluku-sluku Bathok, Jamuran, Bitingan, Ancak-ancak Alis, dan Blarak Sempal.\n\nSetiap "
                 + "ruang dihias sesuai permainannya, memutar tayangan cara bermain, dan memuat papan "
                 + "pelajaran tentang sains, sport science, asal-usul, serta seni dan budaya di baliknya."),
            },
        };

        public static LobbyLesson Gong => new LobbyLesson
        {
            Key = "lobby_gong",
            Title = "Gong Ageng",
            Sections = new[]
            {
                ("GONG & GAMELAN",
                 "Gong ageng adalah gong terbesar dalam seperangkat gamelan Jawa. Suaranya yang paling "
                 + "rendah dan paling panjang menandai akhir satu putaran lagu, yang disebut gongan. "
                 + "Dalam gamelan, gong tidak mengisi melodi; ia memberi bingkai waktu — semua instrumen "
                 + "lain berjalan di antara dua pukulan gong.\n\nGong dibuat dari perunggu, campuran tembaga "
                 + "dan timah, yang ditempa dalam keadaan panas lalu ditala dengan mengikis bagian "
                 + "tengahnya (pencu) sedikit demi sedikit."),
                ("TANDA MULAI",
                 "Di halaman kampung, gong atau kentongan adalah pemanggil: tanda mulai kerja bakti, "
                 + "pertunjukan, dan permainan malam bulan purnama. Bunyi yang terdengar sampai ujung "
                 + "dusun mengumpulkan anak-anak ke tanah lapang.\n\nDalam beberapa dolanan, misalnya "
                 + "Jamuran dan Cublak-cublak Suweng, irama itu digantikan oleh tembang yang dinyanyikan "
                 + "bersama — gong ada di dalam suara anak-anak itu sendiri."),
                ("COBA TABUH",
                 "Berdirilah di depan gong dan tekan Enter (atau ketuk Interaksi). Tabuh berlapis kain "
                 + "akan mengayun dan gong berbunyi.\n\nDengarkan: nada dasar yang dalam, lalu getaran "
                 + "yang perlahan bergelombang. Itu adalah dua nada yang sangat berdekatan saling "
                 + "bertemu — para pembuat gong menyebutnya ombak, dan gong yang baik ombaknya lambat "
                 + "dan tenang."),
            },
        };

        public static LobbyLesson Gasing => new LobbyLesson
        {
            Key = "lobby_gasing",
            Title = "Gasing",
            Sections = new[]
            {
                ("PUTARAN GASING",
                 "Gasing — di Jawa disebut juga gangsingan — adalah salah satu mainan tertua di Nusantara. "
                 + "Badannya dibubut dari kayu keras seperti asam atau sawo, kadang berpaku besi di "
                 + "ujungnya. Tali dililitkan rapat dari ujung ke badan, lalu dilempar dengan sentakan "
                 + "sehingga gasing mendarat berputar.\n\nPatung di tengah lantai ini adalah gasing "
                 + "raksasa: tekan Enter di dekatnya untuk memutarnya, dan tekan lagi untuk mempercepat."),
                ("SAINS DI BALIKNYA",
                 "Gasing berdiri tegak selama ia berputar cepat karena momentum sudut: semakin cepat "
                 + "putarannya, semakin sulit sumbunya diubah. Gesekan dengan tanah dan udara perlahan "
                 + "menghabiskan putaran itu; gasing mulai bergoyang (presesi), lalu rebah.\n\nPembuat "
                 + "gasing mengatur bentuknya agar massa terkumpul di pinggir — badan yang lebar dan "
                 + "berat berputar lebih lama dari yang ramping."),
                ("GASING DI NUSANTARA",
                 "Hampir setiap daerah memiliki gasingnya sendiri: gasing Melayu di Riau dan Kepulauan "
                 + "Riau yang besar dan berlaga dalam pertandingan, gasing Bali, gasing Bugis, hingga "
                 + "gangsingan kecil dari Jawa yang diadu ketahanannya.\n\nPertandingan gasing biasanya "
                 + "mengadu dua hal: siapa yang berputar paling lama, atau siapa yang bisa menyenggol "
                 + "gasing lawan sampai berhenti — ketangkasan tangan bertemu ketelitian membuat."),
            },
        };

        public static LobbyLesson Engklek => new LobbyLesson
        {
            Key = "lobby_engklek",
            Title = "Petak Engklek",
            Sections = new[]
            {
                ("CARA BERMAIN",
                 "Petak digambar di tanah dengan kapur atau pecahan genting. Setiap pemain punya gacuk — "
                 + "kepingan genting atau batu pipih — yang dilempar ke petak pertama. Pemain lalu "
                 + "melompat dengan satu kaki melewati petak yang berisi gacuk, mendarat dengan dua kaki "
                 + "hanya di petak berpasangan, berputar di puncak, dan kembali sambil memungut gacuknya."
                 + "\n\nJika gacuk keluar garis, kaki menyentuh garis, atau pemain hilang keseimbangan, "
                 + "giliran berpindah."),
                ("MAKNA GUNUNG",
                 "Petak paling atas berbentuk setengah lingkaran dan disebut gunung. Pemain yang berhasil "
                 + "menyelesaikan seluruh putaran boleh melempar gacuk dengan membelakangi petak; di mana "
                 + "gacuk jatuh, di situ ia mendapat sawah — petak miliknya yang tidak boleh diinjak "
                 + "lawan.\n\nBentuknya sering dibaca sebagai perjalanan manusia: dari tanah, naik "
                 + "bertahap, menuju gunung, lalu pulang membawa hasil. Di berbagai daerah permainan ini "
                 + "dikenal juga sebagai Sunda Manda atau Taplak."),
                ("COBA SENDIRI",
                 "Petak di lantai ini bisa diinjak. Mulailah dari petak 1 dan berjalanlah ke atas "
                 + "berurutan; setiap petak yang benar akan menyala dan berbunyi, dan gunung di puncak "
                 + "menyambut Anda dengan nada penutup.\n\nSalah urutan? Tidak apa-apa — petak menunggu "
                 + "sampai Anda kembali ke yang benar. Injak petak 1 lagi untuk mengulang dari awal."),
            },
        };

        public static LobbyLesson Tembang => new LobbyLesson
        {
            Key = "lobby_tembang",
            Title = "Tembang Dolanan",
            Sections = new[]
            {
                ("LAGU DOLANAN",
                 "Banyak dolanan Jawa tidak lengkap tanpa lagunya. Tembang dolanan adalah lagu anak "
                 + "berbahasa Jawa dengan laras slendro atau pelog — tangga nada gamelan — yang "
                 + "dinyanyikan sambil bermain. Iramanya mengatur gerak: kapan berjalan melingkar, kapan "
                 + "berhenti, kapan berganti peran.\n\nLiriknya sering terdengar seperti permainan kata, "
                 + "tetapi menyimpan nasihat: tentang mencari rezeki, tentang kematian, tentang "
                 + "kerendahan hati.\n\nTekan Enter di panggung ini untuk mendengar satu bait."),
                ("JAMURAN",
                 "\"Jamuran, jamuran, yo gégé thok / Jamur apa, jamur apa, yo gégé thok / Jamur gajih "
                 + "mbejijih sak ara-ara / Semprat-semprit, jamur apa?\"\n\nAnak-anak bergandengan "
                 + "melingkar di sekeliling satu anak yang dadi (jaga), berjalan sambil menyanyi. Di akhir "
                 + "lagu si penjaga menyebut nama jamur — jamur kendhil, jamur gagak, jamur parut — dan "
                 + "semua harus berpose atau bergerak sesuai jamur itu. Yang gagal menjadi penjaga "
                 + "berikutnya."),
                ("CUBLAK-CUBLAK SUWENG",
                 "\"Cublak-cublak suweng, suwengé ting gelèntèr / Mambu ketundhung gudèl / Pak Empo "
                 + "léra-léré / Sapa ngguyu ndhelikaké / Sir-sir pong dhelé kopong.\"\n\nSatu anak "
                 + "tengkurap sebagai Pak Empo; yang lain meletakkan tangan di punggungnya dan mengedarkan "
                 + "suweng (anting) dari tangan ke tangan mengikuti lagu. Saat lagu berhenti, Pak Empo "
                 + "menebak di tangan siapa suweng disembunyikan — permainan menyimak bunyi dan membaca "
                 + "raut muka."),
                ("SLUKU-SLUKU BATHOK",
                 "\"Sluku-sluku bathok, bathoké éla-élo / Si Rama menyang Sala, oléh-oléhé payung motha / "
                 + "Mak jenthit lolo lobah, wong mati ora obah / Yèn obah medèni bocah, yèn urip goleka "
                 + "dhuwit.\"\n\nDinyanyikan sambil duduk berhadapan, kaki diluruskan, badan diayun ke "
                 + "kanan-kiri, dan pada \"mak jenthit\" semua mematung. Lagu pengantar tidur yang "
                 + "sekaligus mengingatkan: selagi hidup, bekerjalah."),
            },
        };

        public static LobbyLesson Ragam => new LobbyLesson
        {
            Key = "lobby_ragam",
            Title = "Ragam Dolanan",
            Sections = new[]
            {
                ("JENIS PERMAINAN",
                 "Dolanan Jawa dapat dikelompokkan menurut yang paling banyak dipakai: kaki, tangan, "
                 + "suara, atau pikiran.\n\nPermainan lapangan menguji lari dan kelincahan — Gobak Sodor, "
                 + "Bentengan, Lompat Tali, Egrang, Blarak Sempal. Permainan duduk menguji ketelitian jari "
                 + "— Bekelan, Gatheng, Cirak, Bitingan, Benthik. Permainan papan menguji strategi — Dakon, "
                 + "Dam-daman, Dampar. Dan permainan bernyanyi menyatukan semuanya — Jamuran, Cublak-cublak "
                 + "Suweng, Sluku-sluku Bathok, Ancak-ancak Alis."),
                ("ALAT & BAHAN",
                 "Alat dolanan datang dari halaman. Biji sawo, kecik, dan kerang untuk Dakon; batu kali "
                 + "bulat untuk Gatheng; pecahan genting untuk Engklek; bambu untuk Egrang dan Bitingan; "
                 + "kayu ranting untuk Benthik; pelepah kelapa kering untuk Blarak Sempal; karet gelang "
                 + "yang dianyam untuk Lompat Tali.\n\nMeja di depan papan ini memajang beberapa di "
                 + "antaranya. Karena alatnya ada di mana-mana, permainan bisa dimulai kapan saja."),
                ("KAPAN DIMAINKAN",
                 "Sore hari sepulang sekolah dan sehabis mengaji adalah waktu dolanan; halaman rumah dan "
                 + "tanah lapang desa menjadi arenanya. Malam bulan purnama — padhang mbulan — adalah "
                 + "waktu istimewa untuk permainan beramai-ramai seperti Jamuran dan Cublak-cublak Suweng."
                 + "\n\nMusim kemarau sehabis panen membuka lapangan yang luas untuk Gobak Sodor dan "
                 + "Bentengan, sementara musim hujan mengembalikan anak-anak ke emper rumah dengan Dakon "
                 + "dan Bekelan."),
            },
        };

        public static LobbyLesson Filosofi => new LobbyLesson
        {
            Key = "lobby_filosofi",
            Title = "Filosofi Dolanan",
            Sections = new[]
            {
                ("NILAI LUHUR",
                 "Orang tua Jawa dahulu tidak menggurui; mereka membiarkan anak belajar lewat bermain. "
                 + "Dolanan mengajarkan tanpa terasa: sabar menunggu giliran, jujur mengaku kalah, berani "
                 + "menjadi yang dadi, dan tetap berteman setelah bertanding.\n\nTiga hal tumbuh sekaligus: "
                 + "olah raga (badan), olah rasa (perasaan), dan olah pikir (akal). Itulah sebabnya "
                 + "pendidikan tradisional menempatkan dolanan sejajar dengan pelajaran."),
                ("GOTONG ROYONG",
                 "Hampir tidak ada dolanan yang dimainkan sendirian. Jamuran butuh lingkaran, Gobak Sodor "
                 + "butuh dua regu, Bentengan butuh benteng yang dijaga bersama. Sebelum bermain, "
                 + "anak-anak sendiri yang menggambar petak, mencari batu, dan menyepakati aturan.\n\n"
                 + "Di situ gotong royong dipelajari dalam bentuknya yang paling sederhana: permainan tidak "
                 + "dimulai sebelum semua siap, dan tidak menyenangkan jika ada yang ditinggalkan."),
                ("SPORTIVITAS",
                 "Kalah dalam dolanan berarti menjadi penjaga — bukan tersingkir. Yang kalah tetap "
                 + "bermain, bahkan memegang peran paling penting: menjadi Pak Empo, menjadi jamur, "
                 + "menjaga garis Gobak Sodor. Permainan berputar, giliran datang lagi.\n\nAturan "
                 + "dijaga oleh sesama pemain, bukan wasit. Curang berarti tidak diajak bermain esok "
                 + "hari; kejujuran adalah tiket masuk yang paling murah dan paling mahal."),
                ("BELAJAR SAMBIL BERMAIN",
                 "Setiap ruang pamer di lantai atas membaca satu permainan dengan kacamata sains dan "
                 + "sport science: keseimbangan dan pusat massa pada Egrang, koordinasi mata-tangan pada "
                 + "Bekelan, ekologi pertanian pada Dakon dan Ancak-ancak Alis, kerja jantung-paru pada "
                 + "Lompat Tali.\n\nDolanan ternyata sudah lebih dulu tahu apa yang kemudian dijelaskan "
                 + "buku pelajaran. Museum ini hanya mempertemukan keduanya."),
            },
        };

        public static LobbyLesson Etika => new LobbyLesson
        {
            Key = "lobby_etika",
            Title = "Aturan & Etika",
            Sections = new[]
            {
                ("HOMPIMPA & SUIT",
                 "Setiap dolanan dibuka dengan cara yang adil untuk menentukan siapa yang dadi atau "
                 + "siapa yang mulai. Hompimpa — \"hompimpa alaium gambreng\" — dilakukan beramai-ramai "
                 + "dengan telapak tangan menghadap ke atas atau ke bawah; yang berbeda sendiri keluar "
                 + "dari undian. Bila tinggal dua orang, mereka bersuit: jempol (gajah), telunjuk (orang), "
                 + "kelingking (semut).\n\nGajah mengalahkan orang, orang mengalahkan semut, semut "
                 + "mengalahkan gajah — yang besar tidak selalu menang."),
                ("PEMAIN & GILIRAN",
                 "Dakon dimainkan berdua, saling berhadapan. Engklek dan Bekelan dimainkan bergiliran, "
                 + "dua sampai lima orang. Gobak Sodor, Bentengan, dan Jamuran memerlukan dua regu atau "
                 + "satu lingkaran besar. Egrang dilombakan berdampingan, siapa cepat sampai.\n\nGiliran "
                 + "berpindah bila pemain gagal — gacuk keluar garis, biji habis di lumbung lawan, bola "
                 + "bekel memantul dua kali. Yang menunggu giliran bertugas mengawasi: dialah wasitnya."),
                ("MULAI DI MUSEUM INI",
                 "Tiga permainan di lantai atas dapat dimainkan bersama teman melalui jaringan: Dakon "
                 + "(dua pemain), Engklek, dan Egrang (sampai tiga pemain). Masuklah ke pintu permainan, "
                 + "buat ruangan, lalu bagikan kodenya; permainan baru dapat dimulai setelah paling sedikit "
                 + "dua pemain duduk.\n\nSeperti di halaman kampung: tidak ada dolanan yang dimainkan "
                 + "sendirian."),
            },
        };

        public static LobbyLesson[] All() => new[] { Sambutan, Gong, Gasing, Engklek, Tembang, Ragam, Filosofi, Etika };
    }
}
