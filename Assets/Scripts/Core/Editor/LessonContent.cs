namespace Museum.Core.EditorTools
{
    /// <summary>One game's exhibit copy, verbatim from the curriculum spreadsheet.</summary>
    internal struct LessonSource
    {
        public string GameKey;
        public string DisplayName;

        /// <summary>Name of the game's `Vid &lt;Game&gt;` group in Museum.unity — the exhibit
        /// screen the panel hangs beside and whose trigger volume pages it.</summary>
        public string VideoGroup;

        public string Sains;
        public string SportScience;
        public string AsalUsul;
        public string SeniBudaya;
    }

    /// <summary>
    /// All eighteen games' exhibit copy, verbatim from AMS-NAM.xlsx sheet V03-OK, rows 6-23,
    /// columns G/H/I/J. See Assets/Docs/lessons.md for provenance and for why the two question
    /// columns are not here.
    /// </summary>
    /// <remarks>
    /// Editor-side authoring source only: MuseumLessonUIBuilder writes these into the
    /// GameLessonData assets under Assets/Resources/lessons, and the runtime reads those.
    /// Generated from the spreadsheet and checked back against it character for character —
    /// do not retype it by hand.
    /// </remarks>
    internal static class LessonContent
    {
        public const string HeadingSains = "KONSEP SAINS";
        public const string HeadingSportScience = "SPORT SCIENCE";
        public const string HeadingAsalUsul = "ASAL-USUL";
        public const string HeadingSeniBudaya = "SENI & BUDAYA";

        public static LessonSource[] All()
        {
            return new[]
            {
                new LessonSource
                {
                    GameKey = "dakon",
                    DisplayName = "Dakon",
                    VideoGroup = "Vid Dakon",
                    Sains =
                        "Keanekaragaman hayati dan variasi karakteristik biji sebagai representasi sumber "
                        + "daya hayati; hubungan struktur–fungsi pada biji (ukuran, bentuk, massa, tekstur) "
                        + "terhadap pola distribusi dan strategi permainan; klasifikasi dan pengukuran sebagai "
                        + "dasar pengenalan variasi biologis; konsep aliran dan distribusi sumber daya dalam "
                        + "sistem agroekosistem; serta pengembangan keterampilan ilmiah melalui observasi, "
                        + "perbandingan, pengelompokan, dan interpretasi pola permainan.",
                    SportScience =
                        "Anatomi Gerak : Permainan Dakon secara primer mengaktifkan ekstremitas atas, dengan "
                        + "fokus pada tangan dan jari, didukung oleh lengan dan bahu. Koordinasi neuromuskular "
                        + "yang presisi sangat esensial. Sendi yang Terlibat = Sendi Bahu (Glenohumeral): "
                        + "Berperan dalam stabilisasi lengan dan jangkauan minimal untuk memposisikan tangan di "
                        + "atas papan. Sendi Siku (Elbow): Melakukan fleksi dan ekstensi untuk mengatur jarak "
                        + "tangan dari papan, serta pronasi/supinasi lengan bawah untuk orientasi telapak "
                        + "tangan. Sendi Pergelangan Tangan (Radiocarpal): Dipertahankan dalam posisi netral "
                        + "atau sedikit ekstensi untuk stabilitas, mendukung gerakan jari yang presisi. Sendi "
                        + "Jari (MCP, PIP, DIP): Sendi-sendi ini krusial untuk gerakan motorik halus, "
                        + "memungkinkan fleksi dan ekstensi yang presisi untuk menjepit (pincer grasp) dan "
                        + "melepaskan biji. Otot yang Terlibat Gerakan Dakon melibatkan otot ekstrinsik "
                        + "(berasal dari lengan bawah) dan intrinsik (berada di dalam tangan). Biomekanika "
                        + "Gerak = Biomekanika Dakon berfokus pada analisis gaya dan gerakan selama manipulasi "
                        + "biji, didominasi oleh fine motor skills dan merupakan contoh open kinetic chain "
                        + "untuk tangan dan jari. Kontrol Motorik Halus: Kemampuan mengatur kekuatan otot-otot "
                        + "kecil di tangan dan jari untuk gerakan presisi adalah inti permainan",
                    AsalUsul =
                        "Latar belakang dakon ini adalah dari kehidupan petani bagaimana bertani menghasilkan "
                        + "panen sebanyak mungkin dan dimasukkan ke dalam lumbung. Setiap lubang itu dinamakan "
                        + "lumbung sawah. Sawah yang tidak dikerjakan dinamakan bera. Sawah yang hasilnya "
                        + "sangat kurang dinamakan ngacang atau nandur kacang.",
                    SeniBudaya =
                        "Pada papan dakon terdapat tujuh area lumbung kecil yang saling berhadapan, bermakna "
                        + "bahwa semua orang mempunyai kesempatan yang sama. Biji dakon diambil kemudian "
                        + "diletakkan lagi, memiliki makna bahwa dalam hidup kita harus mau memberi dan "
                        + "menerima. Biji dakon diletakkan satu persatu pada area lubang, memiliki makna kita "
                        + "harus menyelesaikan masalah satu persatu asalkan jujur dan baik. Meletakkan biji "
                        + "dakon satu persatu secara memutar ke kanan memiliki arti bahwa jika kita mempunyai "
                        + "rejeki, kita dapat menyimpannya. Kita menyimpan di area lumbung dalam satu kali "
                        + "putaran bermakna jika kita masih mempunyai kelebihan, bisa kita berikan ke yang "
                        + "lain. Pemain tidak diperkenankan meletakkan biji di dalam area lumbung milik lawan "
                        + "(lubang besar), memiliki makna kita harus menghormati orang lain untuk "
                        + "bertanggungjawab terhadap dirinya sendiri. Pemenangnya adalah pemain yang mempunyai "
                        + "jumlah biji paling banyak di lubang besar miliknya, mempunyai arti bahwa mereka yang "
                        + "berhasil adalah yang mempunyai banyak tabungan amalnya.\n\nPermainan ini juga "
                        + "mengajarkan bahwa jika kita mempunyai rejeki, kita dapat membaginya untuk kebutuhan "
                        + "kita sendiri satu per satu (tidak perlu berlebih). Ketika rejeki itu berlebih, kita "
                        + "boleh menyimpannya di lumbung (lobang besar). Lagi-lagi cukup hanya satu. Dan jika "
                        + "kita masih mempunyai lebihnya, kita bagikan ke saudara, tetangga, teman, dan "
                        + "lain-lain.\n",
                },
                new LessonSource
                {
                    GameKey = "engklek",
                    DisplayName = "Engklek",
                    VideoGroup = "Vid Engklek",
                    Sains =
                        "Sistem gerak manusia sebagai hasil interaksi otot, tulang, dan sistem saraf dalam "
                        + "menghasilkan gerakan; transformasi energi selama aktivitas fisik; mekanisme "
                        + "keseimbangan dinamis dan pengaturan pusat massa saat tubuh bertumpu pada satu kaki; "
                        + "serta pengembangan keterampilan ilmiah melalui pengamatan perubahan gerak, "
                        + "pengukuran respons tubuh, dan interpretasi hubungan aktivitas–energi.",
                    SportScience =
                        "Anatomi Gerak = Permainan Engklek secara primer mengaktivasi ekstremitas bawah, "
                        + "dengan sendi panggul, lutut, dan pergelangan kaki bekerja secara sinergis untuk "
                        + "melompat dan menjaga keseimbangan . Otot-otot utama seperti quadriceps dan gluteus "
                        + "maximus memberikan daya dorong eksplosif saat melompat, sementara gastrocnemius dan "
                        + "soleus berperan dalam plantar fleksi untuk take-off . Stabilisasi inti tubuh, "
                        + "terutama oleh otot-otot core, sangat krusial untuk mempertahankan postur tegak dan "
                        + "keseimbangan dinamis selama fase melayang dan mendarat dengan satu kaki. Biomekanika "
                        + "Gerak =  Permainan Engklek menuntut keseimbangan dinamis yang tinggi, di mana pusat "
                        + "gravitasi tubuh harus terus-menerus disesuaikan di atas base of support yang sempit "
                        + "(satu kaki) selama fase melompat dan mendarat . Gaya reaksi tanah (ground reaction "
                        + "force) yang signifikan terjadi saat mendarat, memerlukan aktivasi otot-otot "
                        + "ekstensor lutut dan pergelangan kaki secara eksentrik untuk meredam benturan dan "
                        + "mencegah cedera . Penggunaan ayunan lengan secara efektif membantu menjaga momentum "
                        + "dan stabilitas tubuh, mendukung perpindahan massa dan keseimbangan selama transisi "
                        + "antar kotak",
                    AsalUsul =
                        "Permainan tradisional Engklek memiliki sejarah yang kaya dan diyakini memiliki "
                        + "banyak asal-usul. Salah satu teori menyatakan bahwa permainan ini diperkenalkan ke "
                        + "Indonesia oleh Belanda selama masa penjajahan mereka. Dalam bahasa Belanda, "
                        + "permainan ini dikenal dengan nama “Zondag Maandag,” yang diterjemahkan menjadi "
                        + "“Minggu Senin” dan kemudian diadopsi ke dalam bahasa lokal sebagai “Sunda Manda” "
                        + "atau “Engklek” di beberapa daerah. Teori lain mengatakan bahwa Engklek mirip dengan "
                        + "permainan Inggris kuno yang disebut “Hopscotch,” yang berakar dari Kekaisaran "
                        + "Romawi. Engklek bukanlah permainan yang seragam di seluruh Indonesia; permainan ini "
                        + "memiliki nama dan variasi yang berbeda di berbagai daerah. Misalnya, permainan ini "
                        + "dikenal dengan nama “Setatak” di Riau, “Tejek-tejekan” di Jambi, dan “Marsitekka” di "
                        + "Batak Toba.",
                    SeniBudaya =
                        "Nilai yang terkandung dalam permainan engklek yaitu nilai kedisiplinan ini "
                        + "ditunjukkan secara tidak langsung yaitu saat pemain mematuhi peraturan yang ada pada "
                        + "permainan engklek. Selain itu, ditunjukkan juga saat pemain mengantri menunggu "
                        + "gilirannya untuk bermain. Nilai ketangkasan dapat dilihat dari gerakan anak saat "
                        + "melakukan permainan. Gerakan melompat dengan satu kaki dapat melatih ketangkasan "
                        + "anak dan juga melatih keseimbangan. Nilai Sosial ditunjukan dengan permainan ini "
                        + "dimainkan oleh lebih dari satu orang sehingga mereka harus bersosialisasi dan saling "
                        + "berkomunikasi satu sama lain. Serta nilai kesehatan yang dilihat dari gerakan saat "
                        + "melakukan permainan yaitu melompat-lompat. Saat melompat secara tidak langsung anak "
                        + "telah melakukan olah raga sehingga mendapatkan tubuh yang sehat.\n\nPermainan engklek "
                        + "juga bermakna sebagai perjuangan manusia dalam meraih wilayah kekuasaan dengan "
                        + "aturan tertentu yang harus disepakati untuk mendapatkan tempat berpijak.\n",
                },
                new LessonSource
                {
                    GameKey = "cublak_cublak_suweng",
                    DisplayName = "Cublak-cublak Suweng",
                    VideoGroup = "Vid Cublak",
                    Sains =
                        "Sistem sensorik manusia dalam menerima dan mengolah informasi melalui pendengaran, "
                        + "sentuhan, dan persepsi ruang; mekanisme transduksi rangsang dan integrasi "
                        + "multisensorik dalam pengambilan keputusan; serta pengembangan keterampilan ilmiah "
                        + "melalui observasi respons, identifikasi pola, dan interpretasi informasi sensorik.",
                    SportScience =
                        "Anatomi Gerak= Dalam permainan Cublak-cublak Suweng, pemain yang membungkuk (dadi) "
                        + "mengandalkan kontraksi isometrik otot erector spinae untuk mempertahankan posisi "
                        + "tulang belakang yang terfleksi, didukung oleh stabilisasi hamstrings dan gluteus "
                        + "maximus pada panggul . Pemain yang melingkar secara ritmis menggerakkan tangan dan "
                        + "lengan bawah, melibatkan otot-otot intrinsic hand muscles untuk manipulasi kerikil "
                        + "dan forearm flexors/extensors untuk gerakan pergelangan tangan yang terkoordinasi . "
                        + "Stabilisasi shoulder girdle juga penting untuk menjaga posisi lengan saat tangan "
                        + "diletakkan di punggung pemain dadi, memastikan gerakan mengoper yang lancar. "
                        + "Biomekanika Gerak= Posisi membungkuk pemain dadi menciptakan torque signifikan pada "
                        + "tulang belakang lumbar, menuntut aktivasi otot erector spinae yang kuat untuk "
                        + "menjaga stabilitas postur dan mencegah cedera . Perpindahan kerikil antar tangan "
                        + "melibatkan kinematika tangan yang ritmis dan sinkron dengan lagu, di mana umpan "
                        + "balik taktil dan modulasi gaya sangat penting untuk keberhasilan transfer tanpa "
                        + "terdeteksi . Keseimbangan statis pemain dadi juga krusial, memastikan center of mass "
                        + "tubuh tetap berada di atas base of support yang stabil meskipun ada beban dan "
                        + "gerakan tangan pemain lain di punggungnya.",
                    AsalUsul =
                        "Cublak-Cublak Suweng adalah permainan tradisional yang berasal dari Jawa, Indonesia, "
                        + "khususnya dari daerah Jawa Tengah, Daerah Istimewa Yogyakarta, dan Jawa Timur. "
                        + "Permainan ini dipercaya diciptakan oleh Sunan Giri, salah satu dari Wali Songo "
                        + "(Sembilan Wali) yang memainkan peran penting dalam menyebarkan agama Islam di "
                        + "Indonesia, khususnya di Jawa, sekitar tahun 1442 M.",
                    SeniBudaya =
                        "Lagu “cublak-cublak suweng” \"cublak-cublak suweng, suwenge teng ge/enter, mambu "
                        + "kethundhung gudel Pak empong orong-orong, pak empong orong-orong. Sir sir plek/plong "
                        + "dhele kaplak/gosong, ora enak, sir sir plak/plong dhele keplak/gosong ora enak "
                        + "\".\n\nMakna lirik yang terkandung pada lirik Cublak-cublak suweng adalah untuk mencari "
                        + "harta janganlah menuruti hawa nafsu tetapi semuanya kembali ke hati nurani yang "
                        + "bersih. Kalimat “mambu ketundhung gudèl” Bermakna bahkan orang bodoh minim "
                        + "(pendidikan) mencari harta duniawi tersebut dengan penuh nafsu ego, tindakan "
                        + "korupsi, jual beli jabatan tujuannya untuk mencari kebahagiaan sesaat. “Sopo ngguyu "
                        + "Ndhelikake” diartikan siapa tertawa dia yang menyembunyikan. Mengandung pesan bahwa "
                        + "siapa yang bijaksana, merekalah yang menemukan kebahagian abadi yang hakiki. “Sir” "
                        + "(hati nurani/suara hati) “pong dele kopong” (kedelai kosong tanpa isi). Maksudnya "
                        + "hati nurani yang kosong. Untuk sampai kepada kebahagiaan abadi harus menghindari "
                        + "dari kecintaan kepada kekayaan duniawi, rendah hati, tidak meremehkan orang lain, "
                        + "serta selalu melatih kepekaan Sir/hati nuraninya. Maknanya bahwa untuk sampai kepada "
                        + "tempat harta sejati (Cublak Suweng) atau kebahagiaan sejati, orang harus melepaskan "
                        + "diri dari kecintaan pada harta benda duniawi, mengosongkan diri, rendah hati, tidak "
                        + "merendahkan sesama, serta senantiasa memakai rasa dan mengasah tajam sir-nya atau "
                        + "hati nuraninya.\n",
                },
                new LessonSource
                {
                    GameKey = "egrang",
                    DisplayName = "Egrang",
                    VideoGroup = "Vid Egrang",
                    Sains =
                        "Konsep sains utama dalam game ini adalah keseimbangan tubuh, gerak, gaya, tekanan, "
                        + "energi, dan sistem gerak manusia. Pada Fase D IPA, peserta didik diarahkan untuk "
                        + "memahami pengukuran, gerak dan gaya, tekanan, pesawat sederhana, usaha dan energi, "
                        + "serta hubungan sistem organ dengan fungsinya. Karena itu, permainan egrang virtual "
                        + "sangat cocok digunakan sebagai konteks pembelajaran IPA SMP. Saat karakter berjalan "
                        + "dengan egrang, siswa dapat mempelajari bahwa tubuh membutuhkan keseimbangan agar "
                        + "pusat massa tetap berada di atas titik tumpu. Jika karakter terlalu miring, gaya "
                        + "gravitasi akan membuat tubuh jatuh. Ketika egrang digunakan di permukaan licin, gaya "
                        + "gesek menjadi kecil sehingga karakter lebih mudah tergelincir. Ketika egrang "
                        + "melewati lumpur atau pasir, konsep tekanan dapat dijelaskan melalui luas ujung "
                        + "egrang yang kecil sehingga tekanannya lebih besar. Selain itu, siswa juga dapat "
                        + "mengamati kerja otot, tulang, sendi, sistem saraf, pernapasan, dan peredaran darah "
                        + "saat tubuh melakukan aktivitas fisik.",
                    SportScience =
                        "Anatomi Gerak = Permainan Egrang sangat mengandalkan kekuatan dan koordinasi "
                        + "otot-otot ekstremitas bawah, terutama quadriceps dan hamstrings untuk kontrol lutut, "
                        + "serta gastrocnemius dan soleus untuk stabilisasi pergelangan kaki pada pijakan bambu "
                        + "yang sempit . Otot gluteus medius berperan krusial dalam stabilisasi panggul secara "
                        + "lateral, mencegah tubuh oleng saat berdiri dan melangkah dengan satu egrang yang "
                        + "tinggi . Selain itu, otot-otot inti tubuh (abdominal dan erector spinae) bekerja "
                        + "secara isometrik untuk menjaga postur tegak dan memfasilitasi perpindahan pusat "
                        + "massa tubuh secara efisien. Biomekanika Gerak= Permainan Egrang secara drastis "
                        + "mengurangi base of support (BoS) dan meningkatkan ketinggian center of gravity "
                        + "(CoG), sehingga menuntut penyesuaian keseimbangan yang konstan dan cepat untuk "
                        + "mencegah jatuh . Gerakan berjalan di atas egrang dapat dimodelkan sebagai sistem "
                        + "pendulum terbalik, di mana stabilitas dicapai melalui serangkaian langkah korektif "
                        + "dinamis yang melibatkan koordinasi neuromuskular kompleks . Penggunaan bambu sebagai "
                        + "perpanjangan tungkai meningkatkan leverage dan panjang langkah, namun juga secara "
                        + "signifikan meningkatkan momen gaya pada sendi panggul dan bahu, memerlukan kekuatan "
                        + "otot yang lebih besar untuk kontrol gerakan.",
                    AsalUsul =
                        "Permainan ini diketahui dari alkuturasi budaya Tionghoa dan konon mengajarkan "
                        + "anak-anak nilai filosofis dari peluang masa depan. Permainan ini mendorong pemain "
                        + "untuk berpikir tentang masa depan dan peluang potensial yang ada di depan, seperti "
                        + "halnya panggung bambu yang membutuhkan perencanaan dan eksekusi yang cermat untuk "
                        + "menjaga keseimbangan.",
                    SeniBudaya =
                        "Nilai budaya yang terkandung dalam permainan Egrang adalah kerja keras, keuletan, "
                        + "dan sportivitas. Nilai kerja keras tercermin dari semangat para pemain yang berusaha "
                        + "agar dapat mengalahkan lawannya. Nilai keuletan tercermin dalam proses pembuatan "
                        + "alat yang digunakan untuk berjalan yang memerlukan keuletan dan ketekunan agar "
                        + "seimbang dan mudah digunakan untuk berjalan. Dan nilai sportivitas tercermin tidak "
                        + "hanya dari sikap para pemain yang tidak berbuat curang saat berlangsungnya "
                        + "permainan, tetapi juga mau menerima kekalahan dengan lapang dada.\n\nFilosofi "
                        + "permainan egrang yang pertama yaitu harus punya keseimbangan. Dalam kehidupan pun "
                        + "kita harus seimbang. Tidak boleh berlebihan dalam berbagai hal. Kedua, ketika "
                        + "bermain egrang harus terus berjalan agar tidak jatuh. Begitu juga dalam menjalani "
                        + "hidup, anak-anak harus terus bergerak mencapai tujuan dan cita-cita. Ketiga, dalam "
                        + "bermain, mereka sangat mungkin akan mengalami jatuh,tapi ketika jatuh kita harus "
                        + "bangkit lagi. Begitu pula ketika ada masalah. Kita harus bangkit menghadapi setiap "
                        + "masalah.\n",
                },
                new LessonSource
                {
                    GameKey = "gobak_sodor",
                    DisplayName = "Gobak Sodor",
                    VideoGroup = "Vid Gobak",
                    Sains =
                        "Interaksi sistem gerak dan respons tubuh terhadap perubahan lingkungan; pengaturan "
                        + "kecepatan, arah, dan koordinasi gerakan dalam ruang; mekanisme persepsi dan "
                        + "pengambilan keputusan saat menghadapi stimulus dinamis; serta pengembangan "
                        + "keterampilan ilmiah melalui observasi pola gerak dan analisis strategi adaptif.",
                    SportScience =
                        "Anatomi pada Gobak Sodor= Gobak Sodor melibatkan kerja otot tungkai, otot inti, "
                        + "serta koordinasi saraf dan otot untuk menghasilkan gerakan cepat dan "
                        + "seimbang.Koordinasi neuromuskular sangat penting dalam aktivitas gerak dinamis. "
                        + "Permainan ini mengaktifkan sendi lutut, panggul, dan pergelangan kaki sehingga "
                        + "meningkatkan kekuatan, fleksibilitas, dan kemampuan motorik kasar.  Gerak "
                        + "multidirectional membantu efisiensi neuromuskular tubuh. Aktivitas Gobak Sodor juga "
                        + "meningkatkan kerja jantung dan paru-paru karena dilakukan dengan intensitas tinggi. "
                        + "Biomekanika Gerak = pada Gobak Sodor Gobak Sodor melibatkan sprint, berhenti "
                        + "mendadak, dan perubahan arah yang membutuhkan keseimbangan dan kontrol pusat massa "
                        + "tubuh. Pentingnya stabilitas postural dalam gerak cepat. Gerakan mengejar dan "
                        + "menghindar menunjukkan konsep kecepatan, percepatan, dan perpindahan berat badan "
                        + "secara efisien. Efisiensi gerak dipengaruhi koordinasi otot dan posisi sendi. Pola "
                        + "gerak zig-zag dan lateral dalam Gobak Sodor melatih agility dan keseimbangan dinamis "
                        + "pemain. Perubahan arah cepat meningkatkan koordinasi neuromuskular.",
                    AsalUsul =
                        "Gobak Sodor adalah permainan tradisional yang terisnpirasi dari kegiatan yang "
                        + "dilakukan oleh para prajurit untuk melatih atau meningkatkan kemampuan bertempur "
                        + "mereka selama perang. Permainan ini awalnya disebut “sodoran” dan sejak saat itu "
                        + "berevolusi menjadi versi modern yang dikenal dengan nama Gobak Sodor.",
                    SeniBudaya =
                        "Dalam permainan Gobak Sodor ini sangat dibutuhkan kerja sama tim yang akan membantu "
                        + "menyukseskan tujuan dari kelompok tersebut. Dalam permainan ini memiliki makna bahwa "
                        + "hidup harus memanfaatkan sebuah peluang dengan baik untuk mencapai sebuah "
                        + "keberhasilan. Gobak sodor merupakan permainan yang diibaratkan seperti melewati "
                        + "pintu menyiratkan bahwa bila ada satu pintu yang tertup dalam hidup, maka ada satu "
                        + "pintu lain yang terbuka.",
                },
                new LessonSource
                {
                    GameKey = "bentengan",
                    DisplayName = "Bentengan",
                    VideoGroup = "Vid Bentengan",
                    Sains =
                        "Respons fisiologis tubuh terhadap aktivitas intensitas sedang–tinggi; mekanisme "
                        + "penggunaan energi dan adaptasi tubuh selama aktivitas berulang; pengaruh persepsi "
                        + "ruang dan pengambilan keputusan terhadap efektivitas gerak; serta pengembangan "
                        + "keterampilan ilmiah melalui pengamatan pola aktivitas dan interpretasi perubahan "
                        + "performa.",
                    SportScience =
                        "Anatomi pada Bentengan= Permainan Bentengan melibatkan kerja otot tungkai, otot "
                        + "inti, dan koordinasi tubuh untuk mendukung aktivitas berlari, mengejar, dan "
                        + "menghindar secara cepat. Gerakan tersebut membantu meningkatkan kekuatan otot, "
                        + "keseimbangan, dan koordinasi motorik pemain. Aktivitas dalam Bentengan banyak "
                        + "menggunakan sendi lutut, panggul, dan pergelangan kaki sehingga meningkatkan "
                        + "fleksibilitas serta daya tahan otot tubuh bagian bawah. Permainan ini juga melatih "
                        + "respons neuromuskular melalui perubahan gerak yang cepat dan berulang. Intensitas "
                        + "gerak pada Bentengan dapat meningkatkan kerja sistem kardiovaskular dan pernapasan "
                        + "karena pemain melakukan aktivitas fisik secara aktif selama permainan berlangsung. "
                        + "Kondisi ini membantu meningkatkan kebugaran jasmani dan daya tahan aerobik tubuh. "
                        + "Biomekanika Gerak pada Bentengan= Bentengan melibatkan gerakan sprint, akselerasi, "
                        + "deselerasi, dan perubahan arah yang membutuhkan kontrol keseimbangan tubuh yang "
                        + "baik. Pemain harus mampu mengatur pusat massa tubuh agar tetap stabil saat bergerak "
                        + "cepat. Gerakan menyerang dan menghindar dalam Bentengan menunjukkan penerapan "
                        + "kecepatan, kelincahan, serta perpindahan berat badan secara efisien. Koordinasi "
                        + "antara otot dan sendi sangat penting untuk menghasilkan gerak yang efektif dan aman. "
                        + "Pola gerak multidirectional pada Bentengan membantu meningkatkan agility, reaksi "
                        + "gerak, dan koordinasi neuromuskular pemain. Aktivitas ini juga melatih kemampuan "
                        + "tubuh dalam mempertahankan stabilitas saat bergerak dinamis.",
                    AsalUsul =
                        "Nama “Bentengan” berasal dari kata “benteng” dalam bahasa Indonesia, yang berarti "
                        + "“benteng”. Nama ini mencerminkan elemen strategis dan defensif dari permainan ini. "
                        + "Bentengan diyakini berasal dari strategi bertahan hidup yang digunakan oleh orang "
                        + "Indonesia selama era kolonial. Permainan ini meniru taktik pertahanan yang digunakan "
                        + "oleh masyarakat untuk melindungi diri dari serangan, menggunakan “benteng” yang "
                        + "ditunjuk sebagai zona aman",
                    SeniBudaya =
                        "Permainan benteng diibaratkan dengan tempat menimba ilmu. jika salah satu regu "
                        + "keluar, maka orang tersebut tidak memiliki pegangan ilmu lagi. Sehingga dalam "
                        + "permainan ini kita harus berpegang teguh dalam menjaga dan mnyelamatkan suatu hal "
                        + "yang dimiliki dari serangan. Nilai filosofi lainnya dalam permainan bentengan yakni "
                        + "kita harus berpegang teguh. Manfaat lainnya adalah melatih keprajuritan, kemiliteran "
                        + "dan kelincahan sejak dini.",
                },
                new LessonSource
                {
                    GameKey = "benthik",
                    DisplayName = "Benthik",
                    VideoGroup = "Vid Benthik",
                    Sains =
                        "Hubungan gaya, energi, dan gerak pada objek; pengaruh sudut, arah, dan besar gaya "
                        + "terhadap lintasan dan jarak tempuh; interaksi antara karakteristik material dan "
                        + "hasil gerakan; serta pengembangan keterampilan ilmiah melalui eksperimen sederhana "
                        + "dan interpretasi hubungan sebab–akibat.",
                    SportScience =
                        "Anatomi pada Bethik = Permainan Bethik melibatkan koordinasi otot lengan, bahu, "
                        + "pergelangan tangan, serta otot tungkai untuk melakukan gerakan memukul dan berlari. "
                        + "Aktivitas ini membantu meningkatkan kekuatan otot, koordinasi mata dan tangan, serta "
                        + "kemampuan motorik pemain. Gerakan memukul pada Bethik menggunakan kerja sendi bahu, "
                        + "siku, dan pergelangan tangan yang berfungsi menghasilkan ayunan secara optimal. "
                        + "Selain itu, otot inti juga berperan menjaga keseimbangan tubuh saat melakukan "
                        + "pukulan dan perpindahan gerak. Aktivitas berlari dan bergerak cepat dalam Bethik "
                        + "dapat meningkatkan kerja jantung dan paru-paru sehingga membantu meningkatkan daya "
                        + "tahan fisik. Permainan ini juga mendukung perkembangan kelincahan dan kebugaran "
                        + "jasmani pemain. Biomekanika Gerak pada Bethik = Bethik melibatkan gerak ayunan yang "
                        + "memanfaatkan prinsip gaya, kecepatan, dan koordinasi segmental tubuh untuk "
                        + "menghasilkan pukulan yang efektif. Semakin baik koordinasi gerak tubuh, semakin "
                        + "besar tenaga yang dihasilkan saat memukul. Gerakan memukul dan berlari pada Bethik "
                        + "membutuhkan keseimbangan tubuh serta perpindahan berat badan yang tepat agar gerak "
                        + "menjadi efisien. Posisi tubuh dan sudut ayunan sangat memengaruhi arah serta "
                        + "kekuatan pukulan. Aktivitas mengejar dan menghindar dalam Bethik melatih agility, "
                        + "kecepatan reaksi, dan koordinasi neuromuskular pemain. Pola gerak cepat dan "
                        + "multidirectional membantu meningkatkan kemampuan kontrol tubuh saat bergerak "
                        + "dinamis.",
                    AsalUsul =
                        "Pada permainan ini terjadi benturan antara kedua alat permainan ini sehingga "
                        + "menimbulkan suara \"thik\". Oleh karena itu permainan tersebut disebut dengan benthik. "
                        + "Sejarah perkembangan permainan benthik sudah ada sejak lama dan berasal dari daerah "
                        + "pedesaan.",
                    SeniBudaya =
                        "Filosofi yang terkandung dalam permainan ini bertujuan untuk melatih jiwa "
                        + "sportifitas dan berkompetisi secara jujur, terampil, dan cekatan yang harus "
                        + "ditanamkan sejak kecil. Permainan Benthik tak sekedar menyenangkan, namun di "
                        + "dalamnya juga terkandung falsafah kehidupan yang dapat kita petik dan semai pada "
                        + "kehidupan nyata. ?Hongpimpa Alaium Gambreng?, kalimat yang biasa diucapkan oleh para "
                        + "pemain sebelum permainan dimulai untuk menentukan siapa yang berhak bermain dahulu "
                        + "memiliki makna agung yaitu ?Dari Tuhan, Kembali ke Tuhan, Mari Kita Bermain?. "
                        + "Kalimat ini merupakan sebuah pengingat saat bermain sekalipun bahwa manusia adalah "
                        + "milik Tuhan. Karena kita ada yang memiliki, maka dari itu setiap perbuatan akan "
                        + "dipertanggung-jawabkan kepada Dzat pemilik kita, yaitu Tuhan yang Maha Esa.",
                },
                new LessonSource
                {
                    GameKey = "gatheng",
                    DisplayName = "Gatheng",
                    VideoGroup = "Vid Gatheng",
                    Sains =
                        "Perilaku gerak objek dan koordinasi sensorimotor dalam aktivitas manipulatif; "
                        + "pengaruh bentuk, ukuran, dan massa terhadap akurasi dan kontrol; mekanisme prediksi "
                        + "lintasan berdasarkan pengamatan; serta pengembangan keterampilan ilmiah melalui "
                        + "klasifikasi, pengukuran, dan evaluasi hasil gerakan.",
                    SportScience =
                        "Anatomi pada Gatheng = Permainan Gatheng melibatkan koordinasi otot jari, tangan, "
                        + "dan mata dalam mengambil serta melempar biji gatheng secara tepat. Aktivitas ini "
                        + "membantu meningkatkan keterampilan motorik halus dan koordinasi gerak tubuh. Gerakan "
                        + "menggenggam, melempar, dan menangkap pada permainan Gatheng menggunakan kerja otot "
                        + "lengan bawah, pergelangan tangan, dan jari tangan secara berulang. Hal tersebut "
                        + "dapat meningkatkan fleksibilitas dan ketepatan gerak tangan pemain. Permainan ini "
                        + "juga melatih konsentrasi, keseimbangan postur, dan kontrol gerak tubuh saat pemain "
                        + "duduk atau bergerak mengambil biji gatheng. Aktivitas tersebut mendukung "
                        + "perkembangan koordinasi neuromuskular secara baik. Biomekanika Gerak pada Gatheng=  "
                        + "Gatheng melibatkan gerak manipulatif berupa melempar dan menangkap yang membutuhkan "
                        + "koordinasi waktu, ketepatan, dan kontrol gaya gerak. Pemain harus mampu mengatur "
                        + "kekuatan lemparan agar objek dapat ditangkap kembali dengan baik. Gerakan tangan "
                        + "pada permainan Gatheng menunjukkan prinsip biomekanika tentang koordinasi segmental "
                        + "dan kontrol gerak halus. Posisi jari dan pergelangan tangan sangat memengaruhi "
                        + "akurasi saat mengambil maupun menangkap biji gatheng. Aktivitas berulang dalam "
                        + "permainan Gatheng membantu meningkatkan kecepatan reaksi, koordinasi mata dan "
                        + "tangan, serta efisiensi gerak tubuh. Pola gerak tersebut mendukung kemampuan kontrol "
                        + "motorik halus pemain.",
                    AsalUsul =
                        "Berasal dari Jawa dan alat permainannya berujud batu gatheng. Permainan gatheng "
                        + "mirip sekali dengan permainan bekelan tetapi berbeda alat permainannya. Permainan "
                        + "gatheng dapat disebut permainan Jawa asli dengan bukti diketemukannya batu gatheng "
                        + "milik Raden Ronggo, putera Panembahan Senopati, raja Mataram pertama. Sekarang batu "
                        + "tersebut disimpan di Kotagedhe bekas ibukota Mataram.",
                    SeniBudaya =
                        "Gatheng sudah ada sejak lama, kira-kira pada zaman Mataram (abad XVII). Putra raja "
                        + "Mataram pada saat itu, Raden Rangga, memiliki alat bermain watu gatheng yang "
                        + "berukuran lebih besar dari watu gatheng biasa. Besarnya batu tersebut membuktikan "
                        + "betapa saktinya Raden Rangga. Watu gatheng yang diyakini milik Raden Rangga "
                        + "tersebut, sekarang masih tersimpan di Kotagede, Yogyakarta. Di dalam permainan "
                        + "gatheng terdapat iringan lagu genjeng sambil memukul pelan• pelan lutut kiri peserta "
                        + "yang kalah. Adapun syair lagu tersebut adalah \"genjeng-genjeng debog bosok, jambe "
                        + "wangen, mur murtigung-mur murtigung walang wadung dening cengkung, rondhe-rondhe "
                        + "pira, salah pira luwe, salawe aja na badhe, picak jenggol pira kiye, cakuthu-cakuthu "
                        + "badhoganmu tahu besu aku dewe carang madu\"",
                },
                new LessonSource
                {
                    GameKey = "bekelan",
                    DisplayName = "Bekelan",
                    VideoGroup = "Vid Bekelan",
                    Sains =
                        "Koordinasi gerak halus melalui integrasi sistem saraf dan sistem gerak; hubungan "
                        + "gaya, waktu, dan lintasan objek selama aktivitas lempar–tangkap; kemampuan prediksi "
                        + "dan respons terhadap perubahan posisi objek; serta pengembangan keterampilan ilmiah "
                        + "melalui pengamatan pola dan interpretasi gerakan.",
                    SportScience =
                        "Anatomi pada Bekelan= Permainan Bekelan melibatkan koordinasi otot jari, tangan, dan "
                        + "mata untuk melakukan gerakan melempar, menangkap, dan mengambil biji bekel secara "
                        + "tepat. Aktivitas ini membantu meningkatkan kemampuan motorik halus dan ketepatan "
                        + "gerak. Gerakan pada Bekelan banyak menggunakan otot lengan bawah, pergelangan "
                        + "tangan, dan jari tangan yang bekerja secara berulang untuk menjaga kontrol gerakan. "
                        + "Selain itu, koordinasi saraf dan otot sangat berperan dalam menjaga konsentrasi dan "
                        + "akurasi pemain. Permainan Bekelan juga melatih keseimbangan postur tubuh dan kontrol "
                        + "gerak saat pemain duduk atau bergerak mengambil biji bekel. Aktivitas tersebut "
                        + "mendukung perkembangan koordinasi neuromuskular dan konsentrasi gerak. Biomekanika "
                        + "Gerak pada Bekelan= Bekelan melibatkan gerak manipulatif berupa melempar dan "
                        + "menangkap bola kecil yang membutuhkan pengaturan gaya, waktu, dan koordinasi gerak "
                        + "secara tepat. Ketepatan gerakan sangat dipengaruhi oleh sinkronisasi mata dan "
                        + "tangan. Gerakan mengambil biji bekel menunjukkan prinsip biomekanika tentang kontrol "
                        + "gerak halus dan koordinasi segmental tangan. Posisi jari dan kecepatan gerakan "
                        + "menentukan keberhasilan pemain dalam mengambil objek tanpa kehilangan kontrol bola. "
                        + "Aktivitas berulang dalam Bekelan membantu meningkatkan kecepatan reaksi, ketepatan "
                        + "gerak, dan efisiensi koordinasi neuromuskular. Pola gerak ini mendukung perkembangan "
                        + "keterampilan motorik halus dan kontrol tubuh pemain.",
                    AsalUsul =
                        "Permainan bekel merupakan permainan tradisional yang mendapat pengaruh dari budaya "
                        + "Belanda, sebab kata bekel sendiri berasal dari bahasa Belanda bikkelspel atau "
                        + "bikkelen. Permainan ini menjadi salah satu permainan populer yang dapat dimainkan "
                        + "oleh laki-laki dan kebanyakan dimainkan oleh perempuan dengan minimal 2 pemain mulai "
                        + "dari kalangan anak-anak hingga dewasa.",
                    SeniBudaya =
                        "Filosofi pada permainan bekelan yaitu saat bola dilempar ke atas, dimaknai sebagai "
                        + "orang hidup harus selalu ingat pada Tuhan Yang maha Esa, setelah itu bola akan "
                        + "kembali turun dan ditangkap oleh pemainnya menggambarkan bahwa selain ingat pada "
                        + "Tuhan Yang Maha Esa, manusia juga harus ingat pada sesame manusia untuk hidup "
                        + "bersosialisasi. Setelah itu, biji atau bekel yang ada diraup jadi satu dalam "
                        + "genggaman dan disebar, maknanya adalah bahwa manusia hidup harus ingat pada "
                        + "sesamanya dan selalu berbuat baik pada sesamanya yang berasal dari Tuhan yang "
                        + "“Satu”. Setelah bekel disebar, bola kembali dilempar ke atas sambil membolak-balik "
                        + "timbel (bekel) dari urutan 1 sampai 5. Hal ini menggambarkan bahwa manusia hidup "
                        + "harus hidup sesuai aturan dan harus bisa mengendalikan hawa nafsu, karena sesuatu "
                        + "hal dalam hidup manusia akan berhubungan dengan hal yang lain.\n\n",
                },
                new LessonSource
                {
                    GameKey = "lompat_tali",
                    DisplayName = "Lompat Tali",
                    VideoGroup = "Vid Lompat",
                    Sains =
                        "Transformasi energi dalam aktivitas gerak berulang; hubungan aktivitas otot, "
                        + "kebutuhan oksigen, dan respons fisiologis tubuh; mekanisme adaptasi terhadap ritme "
                        + "dan intensitas aktivitas; serta pengembangan keterampilan ilmiah melalui pengukuran "
                        + "respons tubuh dan interpretasi hubungan aktivitas–energi.",
                    SportScience =
                        "Anatomi pada Lompat Tali= Permainan Lompat Tali melibatkan kerja otot tungkai, "
                        + "seperti quadriceps, hamstring, dan gastrocnemius, untuk menghasilkan gerakan "
                        + "melompat secara berulang. Aktivitas ini membantu meningkatkan kekuatan otot kaki dan "
                        + "daya tahan tubuh. Gerakan melompat menggunakan koordinasi sendi lutut, panggul, dan "
                        + "pergelangan kaki untuk menjaga keseimbangan serta stabilitas tubuh saat mendarat. "
                        + "Otot inti juga berperan penting dalam mempertahankan postur tubuh selama permainan "
                        + "berlangsung. Aktivitas Lompat Tali dapat meningkatkan kerja jantung dan paru-paru "
                        + "karena dilakukan dengan intensitas gerak yang kontinu. Permainan ini mendukung "
                        + "peningkatan kebugaran aerobik, koordinasi tubuh, dan kelincahan pemain. Biomekanika "
                        + "Gerak pada Lompat Tali = Lompat Tali melibatkan prinsip biomekanika berupa gaya "
                        + "dorong, keseimbangan, dan koordinasi gerak tubuh saat melakukan tolakan dan "
                        + "pendaratan. Pemain harus mampu mengontrol pusat massa tubuh agar tetap stabil ketika "
                        + "melompat. Gerakan melompat membutuhkan perpindahan energi dari otot tungkai untuk "
                        + "menghasilkan daya ledak dan ritme gerak yang konsisten. Posisi kaki dan sudut lutut "
                        + "sangat memengaruhi efisiensi serta keamanan saat mendarat. Aktivitas melompat "
                        + "berulang dalam permainan ini membantu meningkatkan agility, koordinasi "
                        + "neuromuskular, dan kemampuan reaksi tubuh. Pola gerak ritmis tersebut juga mendukung "
                        + "efisiensi gerakan dan kontrol keseimbangan dinamis pemain.",
                    AsalUsul =
                        "Lompat tali atau dalam bahasa Inggris disebut skipping, skipping rope atau skipping "
                        + "jump merupakan olahraga dan permainan yang sudah ada sejak zaman dahulu. Tidak "
                        + "diketahui siapa yang menciptakan permainan lompat karet atau lompat tali. Sekitar "
                        + "tahun 1600 SM, permainan ini sudah dimainkan di Mesir.\n\nPendapat lain menyatakan "
                        + "bahwa permainan ini berasal dari China dan Jepang. Di samping itu, ada juga yang "
                        + "berpendapat bahwa lompat tali dimainkan oleh Suku Aborigin di Australia. Mereka "
                        + "memainkan lompat tali menggunakan tali dari tanaman rambat dan bambu hutan. Pada "
                        + "abad ke-19, lompat tali dimainkan oleh anak- anak perempuan dengan tiga pemain, dua "
                        + "pemain memegang dan memutar tali, sedangkan satu pemain bertugas melompat di tengah "
                        + "sambil bernyanyi.\n",
                    SeniBudaya =
                        "Filosofi dari permainan ini adalah setiap kali kita selesai menaklukkan tantangan, "
                        + "maka akan ada tantangan lain yang lebih tinggi atau sulit dari sebelumnya. Oleh "
                        + "karena itu, kita tidak boleh menyerah dan harus tetap berusaha.",
                },
                new LessonSource
                {
                    GameKey = "dam_daman",
                    DisplayName = "Dam-daman",
                    VideoGroup = "Vid Damdaman",
                    Sains =
                        "Pengenalan pola dan hubungan antarvariabel dalam pengambilan keputusan; kemampuan "
                        + "prediksi berbasis observasi; mekanisme berpikir logis dalam memilih strategi; serta "
                        + "pengembangan keterampilan ilmiah melalui identifikasi pola dan penyusunan penjelasan "
                        + "berbasis bukti.",
                    SportScience =
                        "Anatomi pada Dam-daman Permainan = Dam-daman melibatkan koordinasi otot jari, "
                        + "tangan, dan mata dalam memindahkan bidak secara tepat pada papan permainan. "
                        + "Aktivitas ini membantu meningkatkan kemampuan motorik halus dan konsentrasi pemain. "
                        + "Gerakan memegang dan menggeser bidak menggunakan kerja otot lengan bawah, "
                        + "pergelangan tangan, dan jari tangan secara terkontrol. Selain itu, postur duduk saat "
                        + "bermain juga melibatkan otot inti untuk menjaga kestabilan tubuh. Permainan ini "
                        + "mendukung koordinasi neuromuskular melalui aktivitas berpikir dan gerak tangan yang "
                        + "dilakukan secara bersamaan. Konsentrasi dan ketepatan gerak menjadi faktor penting "
                        + "dalam menjalankan strategi permainan. Biomekanika Gerak pada Dam-daman= Dam-daman "
                        + "melibatkan gerak manipulatif sederhana berupa mengambil dan memindahkan bidak dengan "
                        + "kontrol gerak yang presisi. Ketepatan posisi tangan dan jari memengaruhi efisiensi "
                        + "gerakan pemain. Gerakan pada permainan ini menunjukkan prinsip biomekanika tentang "
                        + "koordinasi segmental tangan dan kontrol gerak halus. Pengaturan gaya gerak yang "
                        + "tepat membantu pemain memindahkan bidak dengan stabil dan akurat. Aktivitas berulang "
                        + "dalam Dam-daman membantu meningkatkan koordinasi mata dan tangan, kecepatan reaksi, "
                        + "serta efisiensi kontrol motorik halus. Pola gerak tersebut mendukung kemampuan "
                        + "konsentrasi dan ketepatan gerak pemain.",
                    AsalUsul =
                        "Permainan dam-daman merupakan permainan tradisional yang dibesarakan di Pulau Jawa "
                        + "dan memiliki aturan mirip dengan permainan catur. Akan tetapi, Dam-Daman tidak "
                        + "memerlukan pion mewah seperti catur. Kita hanya perlu menyiapkan 16-20 pion kecil "
                        + "yang bisa dari apa saja, seperti biji-bijian atau batu-batuan. Jadi, terlihat "
                        + "mengapa permainan ini sering dimainkan oleh anak-anak kecil zaman dulu karena "
                        + "permainannya sederhana dan mudah untuk mencari alat-alat yang dibutuhkan Dam-daman "
                        + "di mana pun.",
                    SeniBudaya =
                        "Semua manusia hakikatnya sama di sisi Tuhan Yang Maha Esa. Tuhan tidak membedakan "
                        + "manusia berdasarkan pangkat dan jabatan. Selain itu, bermain dam-daman juga "
                        + "mengajarkan kepada kita agar dalam melangkah harus hati-hati. Meski bebas dan banyak "
                        + "pilihan, tetapi untuk setiap langkah yang kita jalankan akan ada dampak yang "
                        + "ditimbulkan. Bisa positif dan juga bisa negatif. Nah sebagai manusia biasa, kita "
                        + "juga harus berani dalam menghadapi cobaan dari Tuhan supaya seseorang bisa lebih "
                        + "maju. Di samping itu semua, bermain dam-daman juga bisa bermanfaat untuk melatih "
                        + "kecerdasan dan melatih kita dalam mengambil resiko di setiap pilihan hidup kita.",
                },
                new LessonSource
                {
                    GameKey = "cirak",
                    DisplayName = "Cirak",
                    VideoGroup = "Vid Cirak",
                    Sains =
                        "Hubungan antara gaya, arah, dan ketepatan gerakan dalam menghasilkan hasil aktivitas "
                        + "yang diinginkan; mekanisme interaksi sistem sensorik dan sistem gerak dalam "
                        + "mengendalikan posisi serta lintasan gerakan; pengaruh perubahan besar gaya, sudut, "
                        + "dan koordinasi terhadap hasil permainan; serta pengembangan keterampilan ilmiah "
                        + "melalui pengamatan pola gerak, pengukuran hasil, dan interpretasi hubungan "
                        + "sebab–akibat.",
                    SportScience =
                        "Anatomi pada Cirak = Permainan Cirak melibatkan koordinasi otot tangan, jari, mata, "
                        + "dan tungkai dalam aktivitas melempar, menangkap, serta bergerak cepat. Aktivitas ini "
                        + "membantu meningkatkan kemampuan motorik kasar dan halus pemain. Gerakan melempar dan "
                        + "menangkap menggunakan kerja otot bahu, lengan, pergelangan tangan, dan jari secara "
                        + "terkoordinasi. Selain itu, otot inti dan tungkai membantu menjaga keseimbangan tubuh "
                        + "saat bergerak dan menghindar. Aktivitas fisik dalam Cirak dapat meningkatkan "
                        + "kelincahan, koordinasi tubuh, dan daya tahan fisik pemain karena melibatkan gerak "
                        + "aktif secara berulang. Permainan ini juga mendukung perkembangan respons "
                        + "neuromuskular dan konsentrasi gerak. Biomekanika Gerak pada Cirak = Cirak melibatkan "
                        + "prinsip biomekanika berupa gaya lemparan, kecepatan gerak, dan koordinasi tubuh saat "
                        + "menangkap maupun menghindari objek. Ketepatan arah dan kekuatan gerakan sangat "
                        + "memengaruhi keberhasilan pemain. Gerakan melempar dan bergerak dalam Cirak "
                        + "membutuhkan perpindahan berat badan dan keseimbangan tubuh yang baik agar gerak "
                        + "tetap efisien. Posisi tubuh dan koordinasi sendi membantu menghasilkan gerakan yang "
                        + "cepat dan terkontrol. Aktivitas reaktif dalam permainan Cirak membantu meningkatkan "
                        + "agility, waktu reaksi, dan koordinasi neuromuskular pemain. Pola gerak cepat dan "
                        + "multidirectional mendukung kemampuan kontrol tubuh secara dinamis.",
                    AsalUsul =
                        "Sejak abad pertengahan permainan cirak atau kelereng ini sudah ada dan seringkali "
                        + "dimainkan oleh kalangan aristokrat dan bangsawan. Permainan ini tidak hanya terkenal "
                        + "di kalangan masyarakat kita saja, di Perancis pun permainan ini ternyata sangat "
                        + "digemari oleh kalangan di sana dan mereka memanggilnya dengan sebutan Pentaque. "
                        + "Bedanya, jika permainan kelereng menggunakan gundu yang berukuran kecil, Pentaque "
                        + "memerlukan dua jenis bola yang mempunyai ukuran yang cukup besar yang terbuat dari "
                        + "kayu jati dan baja. Pentaque ini pertama kali diperkenalkan oleh Suku Gaule(Perancis "
                        + "Kuno). Dari Perancis permainan ini menyebar ke wilayah lainnya seperti Yunani dan "
                        + "Mesir melalui orang-orang Romawi. Seperti halnya Nekeran, Pentaque awalnya juga "
                        + "merupakan permainan untuk mengisi waktu luang. Sejarah pun berlanjut hingga sampai "
                        + "ke zaman Renaissance atau pencerahan. Pentaquemenjadi mainan di kalangan aristokrat "
                        + "dan bangsawan bahkan kabarnya pernah disejajarkan dengan olahraga Tennis yang "
                        + "dipandang cukup elit di masa itu. Yang diperbolehkan untuk bermain olahraga itu "
                        + "hanyalah orang-orang tertentu saja.\n\nTerhitung sejak tahun 1850, sebuah organisasi "
                        + "sosial Clos Jouve memperkenalkan kembali Pentaque yang semakin hari kian dilupakan "
                        + "oleh masyarakat. Menginjak abad ke-20 permainan ini mulai dipatenkan seiring dengan "
                        + "semakin banyaknya bermunculan klub-klub Pentaque sebagai pelestarian kebudayaan "
                        + "tradisional.\n\n",
                    SeniBudaya =
                        "Filosofi dari permainan cirak atau kelereng yaitu anak-anak menjadi konsentrasi "
                        + "dalam mencapai sasaran atau keinginan, menjadikan anak-anak lebih sabar dan tidak "
                        + "gegabah dalam mengambil putusan, melatih anak dalam menjunjung kebersamaan, "
                        + "sportivitas dan melatih bekerja sama dengan baik dalam tim.",
                },
                new LessonSource
                {
                    GameKey = "dampar",
                    DisplayName = "Dampar",
                    VideoGroup = "Vid Dampar",
                    Sains =
                        "Pola perpindahan dan pengambilan keputusan dalam ruang sebagai hasil interaksi "
                        + "antara persepsi lingkungan dan respons gerakan; mekanisme adaptasi terhadap "
                        + "perubahan posisi, arah, dan strategi selama permainan; hubungan antara keteraturan "
                        + "pola gerak dengan efektivitas tindakan; serta pengembangan keterampilan ilmiah "
                        + "melalui observasi, identifikasi pola, dan evaluasi strategi berdasarkan bukti.",
                    SportScience =
                        "Anatomi pada Dampar= Permainan Dampar melibatkan koordinasi otot tungkai, tangan, "
                        + "dan mata dalam aktivitas melompat, berlari, dan menjaga keseimbangan tubuh. "
                        + "Aktivitas ini membantu meningkatkan kekuatan otot serta kemampuan motorik pemain. "
                        + "Gerakan melompat dan berpindah pada permainan Dampar menggunakan kerja sendi lutut, "
                        + "panggul, dan pergelangan kaki secara aktif. Otot inti juga berperan menjaga "
                        + "stabilitas tubuh saat pemain bergerak cepat dan berubah arah. Aktivitas fisik dalam "
                        + "Dampar dapat meningkatkan kelincahan, koordinasi tubuh, dan daya tahan fisik karena "
                        + "dilakukan secara dinamis dan berulang. Permainan ini mendukung perkembangan "
                        + "keseimbangan dan kontrol gerak pemain. Biomekanika Gerak pada Dampar= Dampar "
                        + "melibatkan prinsip biomekanika berupa tolakan, keseimbangan, dan perpindahan pusat "
                        + "massa tubuh saat melompat maupun berpindah posisi. Pemain harus mampu mengontrol "
                        + "tubuh agar tetap stabil selama bergerak. Gerakan berpindah dan menghindar "
                        + "membutuhkan koordinasi antara kekuatan otot dan pengaturan posisi sendi untuk "
                        + "menghasilkan gerak yang efisien. Kecepatan dan ketepatan langkah sangat memengaruhi "
                        + "keberhasilan permainan. Aktivitas gerak cepat dan multidirectional dalam Dampar "
                        + "membantu meningkatkan agility, koordinasi neuromuskular, dan kemampuan reaksi tubuh. "
                        + "Pola gerak tersebut juga mendukung efisiensi gerakan dan keseimbangan dinamis "
                        + "pemain.",
                    AsalUsul =
                        "Permainan Batu Dampar merupakan salah satu olahraga tradisional asli kota "
                        + "Tanjungbalai yang sudah mengakar dalam kehidupan masyarakat pesisir pantai oleh dua "
                        + "kelompok atau tim yang terdiri dari 3 (tiga) orang.Olahraga tradisional ini disebut "
                        + "batu peredam karena menggunakan alat utama yang terbuat dari batu yang sering "
                        + "terdampar (tersebar) di jalan atau pekarangan diameter antara 8-10 cm, dan terdapat "
                        + "permainan yang mirip dengan permainan batu tanjungbalai Batu Dampar",
                    SeniBudaya =
                        "Permainan gamparan terkandung norma-norma yang dipelajari oleh anak-anak tersebut, "
                        + "misalnya bersosialisasi dengan teman sebaya dan harus bermain secara sportif dan "
                        + "jujur.",
                },
                new LessonSource
                {
                    GameKey = "sluku_sluku_bathok",
                    DisplayName = "Sluku-sluku Bathok",
                    VideoGroup = "Vid Sluku",
                    Sains =
                        "Interaksi antara rangsangan suara, ritme, dan respons tubuh dalam menghasilkan pola "
                        + "gerakan terkoordinasi; mekanisme persepsi pendengaran dan sinkronisasi gerak "
                        + "terhadap stimulus berulang; hubungan antara ritme, keteraturan gerakan, dan adaptasi "
                        + "respons tubuh; serta pengembangan keterampilan ilmiah melalui pengamatan pola "
                        + "respons, interpretasi hubungan stimulus–respons, dan refleksi terhadap fenomena "
                        + "gerak kolektif.",
                    SportScience =
                        "Anatomi pada Sluku-Sluku Bathok= Permainan Sluku-Sluku Bathok melibatkan koordinasi "
                        + "otot tangan, kaki, dan tubuh melalui gerakan tepukan, ayunan, dan perubahan posisi "
                        + "tubuh secara ritmis. Aktivitas ini membantu meningkatkan koordinasi motorik dan "
                        + "keseimbangan tubuh pemain. Gerakan dalam permainan ini menggunakan kerja otot "
                        + "lengan, pergelangan tangan, dan tungkai yang dilakukan secara berulang mengikuti "
                        + "irama lagu. Otot inti juga berperan menjaga stabilitas postur tubuh selama permainan "
                        + "berlangsung. Aktivitas ritmis pada Sluku-Sluku Bathok dapat membantu meningkatkan "
                        + "koordinasi neuromuskular, konsentrasi, dan kontrol gerak tubuh. Permainan ini juga "
                        + "mendukung perkembangan fleksibilitas dan keterampilan motorik anak. Biomekanika "
                        + "Gerak pada Sluku-Sluku Bathok= Sluku-Sluku Bathok melibatkan gerakan ritmis yang "
                        + "membutuhkan koordinasi waktu, keseimbangan, dan sinkronisasi gerak tubuh. Ketepatan "
                        + "gerak sangat dipengaruhi oleh kesesuaian ritme dan kontrol tubuh pemain. Gerakan "
                        + "tepukan dan ayunan tangan menunjukkan prinsip biomekanika tentang koordinasi "
                        + "segmental dan efisiensi gerak berulang. Posisi tubuh yang stabil membantu "
                        + "menghasilkan gerakan yang lebih terkontrol dan harmonis. Aktivitas gerak berirama "
                        + "dalam permainan ini membantu meningkatkan koordinasi neuromuskular, reaksi gerak, "
                        + "dan keseimbangan dinamis. Pola gerak ritmis tersebut juga mendukung efisiensi "
                        + "kontrol motorik pemain.",
                    AsalUsul =
                        "Sluku-sluku bathok adalah lagu dolanan yang berasal dari Jawa Tengah yang mengandung "
                        + "unsur religi. hal ini karena lagu sluku-sluku bathok diciptakan oleh salah satu wali "
                        + "songo, yaitu Sunan Kalijogo. lagu ini digunakan sunan kalijogo untuk menyebarkan "
                        + "agama Islam.",
                    SeniBudaya =
                        "Lagu sluku-sluku bathok :\nSluku Sluku Bathok Bathoke Ela Elo\nSi Rama Menyang Sala "
                        + "Oleh olehe Payung Mutho Mak Jenthit Lolo Lo Bah Yen Mati Ora Obah\nYen Obah Medeni "
                        + "Bocah Yen Urip Goleko Duwit\n\nMakna dalam syair/tembanag tersebut:\n1.	‘Sluku Sluku "
                        + "Bathok’ bermakna bahwa hidup tak hanya soal bekerja, sehingga seseorang perlu "
                        + "mengistirahatkan kepala (pikiran) agar jiwa, dan raga kita dapat kembali bekerja "
                        + "dengan maksimal esok hari.\n2.	‘Bathoke Ela Elo’ bermakna bahwa pikiran kita harus "
                        + "selalu mengingat lafadz dzikir “Laa Ilaaha Illallah” agar lebih tenang dan tentram "
                        + "dalam menjalani kehidupan.\n3.	‘Si Rama Menyang Sala’ mengambil makna dari kata "
                        + "“siram” yang berarti mandi atau bersuci, “menyang” yang artinya menuju, dan Solo "
                        + "yang dimaknai dengan salat. Sehingga lirik tersebut meminta kita untuk menyucikan "
                        + "diri untuk mendirikan salat.\n4.	‘Oleh olehe Payung Mutho’ bermakna bahwa ibadah yang "
                        + "kita lakukan akan membuat kita mendapatkan “payung” yang melambangkan perlindungan "
                        + "dari Tuhan.\n5.	‘Mak Jenthit Lolo Lo Bah’ bermakna bahwa waktu menjelang kematian tak "
                        + "akan bergerak maju ataupun mundur sehingga kita harus selalu mendekatkan diri kepada "
                        + "Tuhan.\n6.	‘Yen Mati Ora Obah’ bermakna bahwa waktu setelah kematian tidak ada lagi "
                        + "yang bisa diubah.\n\n7.	‘Yen Obah Medeni Bocah’ bermakna bahwa jika yang sudah mati "
                        + "akan dihidupkan kembali maka akan menakutkan.\n8.	‘Yen Urip Goleko Duwit’ bermakna "
                        + "bahwa manusia harus memanfaatkan waktu dengan baik, dengan beribadah, beramal, dan "
                        + "bekerja agar tak menyesal di kemudian hari.\n\n",
                },
                new LessonSource
                {
                    GameKey = "jamuran",
                    DisplayName = "Jamuran",
                    VideoGroup = "Vid Jamuran",
                    Sains =
                        "Pengenalan pola dan respons terhadap perubahan stimulus dalam aktivitas kelompok; "
                        + "mekanisme persepsi ruang dan koordinasi gerak kolektif; hubungan ritme dan "
                        + "sinkronisasi terhadap keteraturan gerakan; serta pengembangan keterampilan ilmiah "
                        + "melalui observasi pola interaksi dan interpretasi dinamika kelompok.",
                    SportScience =
                        "Anatomi pada Jamuran = Permainan Jamuran melibatkan koordinasi otot tungkai, tangan, "
                        + "dan tubuh dalam aktivitas berjalan, berputar, serta bergerak mengikuti irama "
                        + "permainan. Aktivitas ini membantu meningkatkan koordinasi motorik dan keseimbangan "
                        + "tubuh pemain. Gerakan berputar dan berpindah posisi menggunakan kerja otot kaki, "
                        + "panggul, dan otot inti untuk menjaga stabilitas tubuh selama permainan berlangsung. "
                        + "Selain itu, koordinasi saraf dan otot berperan penting dalam menjaga ritme gerakan "
                        + "pemain. Aktivitas dalam Jamuran mendukung perkembangan kelincahan, fleksibilitas, "
                        + "dan kontrol gerak tubuh melalui pola gerak yang dilakukan secara berulang. Permainan "
                        + "ini juga membantu meningkatkan konsentrasi dan koordinasi neuromuskular. Biomekanika "
                        + "Gerak pada Jamuran= Jamuran melibatkan gerakan ritmis dan perpindahan posisi yang "
                        + "membutuhkan keseimbangan serta kontrol pusat massa tubuh. Pemain harus mampu menjaga "
                        + "stabilitas saat bergerak memutar dan berpindah arah. Gerakan berjalan melingkar dan "
                        + "berhenti secara tiba-tiba menunjukkan prinsip biomekanika tentang koordinasi gerak, "
                        + "keseimbangan, dan pengaturan gaya tubuh. Ketepatan langkah dan posisi tubuh "
                        + "memengaruhi efisiensi gerakan pemain. Aktivitas gerak dinamis dalam Jamuran membantu "
                        + "meningkatkan agility, koordinasi neuromuskular, dan kemampuan reaksi tubuh terhadap "
                        + "perubahan gerak. Pola gerak berirama tersebut juga mendukung efisiensi kontrol "
                        + "motorik pemain.",
                    AsalUsul =
                        "Jamuran merupakan salah satu lagu atau tembang dolanan yang berasal dari tanah Jawa. "
                        + "Umumnya, lagu dolanan satu ini digunakan untuk mengiringi permainan dengan nama yang "
                        + "sama. Lagu atau tembang jamuran sendiri aslinya berasal dari Jawa Timur dan "
                        + "diciptakan oleh Sunan Giri sebagai media penyebaran agama islam. Lagu \"Jamuran\" "
                        + "berasal dari Jawa Tengah dan merupakan karya ciptaan Ki Hadi Sukatno. Lagu ini cukup "
                        + "singkat dan hanya terdiri dari empat baris. Isi liriknya pun menggambarkan permainan "
                        + "\"Jamuran\" yang intinya menirukan gaya dari jenis-jenis jamur.",
                    SeniBudaya =
                        "Adapun lirik dari tembang dolanan Jamuran sebagai berikut:\nJamuran (jamuran)\nJamuran "
                        + "ya gege thok (jamuran ya bohongan)\nJamur apa ya gege thok (jamur apa yang "
                        + "bohong)\nJamur gajih mbejijih sa ara-ara (jamur lemak yang lembek menyelimuti "
                        + "padang)\nSira mbedhek jamur apa? (kamu menebak jamur apa?)\n\nTembang dolanan ini "
                        + "mengajarkan anak-anak untuk mengikuti arahan pemimpin ketika mereka berada di "
                        + "masyarakat. Selain itu, karya ini mengajarkan kita cara hidup yang lebih "
                        + "bersosialisasi dan bermasyarakat. Dengan berpartisipasi dalam permainan Jamuran, "
                        + "kita juga dididik untuk menghadapi hukuman jika melakukan kesalahan. Selain "
                        + "mengajarkan kita tanggung jawab, daya ingat kita dan apa yang kita ketahui juga "
                        + "dilatih. Oleh karena itu, \"Sira mbadhé jamur apa?\" adalah pertanyaan yang muncul "
                        + "setelah lirik dan mengajak kita untuk mempelajari berbagai nama jamur. Banyak "
                        + "variasi jenis jamur ini bisa menjadi pelajaran hidup bagi kita semua. Ada jamur yang "
                        + "dapat diolah menjadi sayur dan bermanfaat bagi kehidupan, tetapi ada juga jamur yang "
                        + "beracun.\n",
                },
                new LessonSource
                {
                    GameKey = "bitingan",
                    DisplayName = "Bitingan",
                    VideoGroup = "Vid Bitingan",
                    Sains =
                        "Hubungan antara gaya, arah, dan lintasan gerak; mekanisme kontrol dan ketepatan "
                        + "gerakan; pengaruh perubahan posisi terhadap hasil aktivitas; serta pengembangan "
                        + "keterampilan ilmiah melalui pengukuran, pengamatan pola, dan evaluasi hubungan "
                        + "sebab–akibat.",
                    SportScience =
                        "Anatomi pada Bitingan= Permainan Bitingan melibatkan koordinasi otot tangan, jari, "
                        + "mata, dan tungkai dalam aktivitas melempar, mengambil, dan bergerak cepat. Aktivitas "
                        + "ini membantu meningkatkan kemampuan motorik halus maupun kasar pemain. Gerakan "
                        + "melempar dan mengambil benda menggunakan kerja otot bahu, lengan, pergelangan "
                        + "tangan, dan jari secara terkoordinasi. Selain itu, otot tungkai dan inti tubuh "
                        + "membantu menjaga keseimbangan saat pemain bergerak dan berpindah posisi. Aktivitas "
                        + "dalam Bitingan dapat meningkatkan kelincahan, konsentrasi, dan koordinasi "
                        + "neuromuskular karena dilakukan dengan gerakan cepat dan berulang. Permainan ini juga "
                        + "mendukung perkembangan kontrol gerak tubuh pemain. Biomekanika Gerak pada Bitingan= "
                        + "Bitingan melibatkan prinsip biomekanika berupa gaya lemparan, ketepatan gerak, dan "
                        + "koordinasi tubuh saat melempar maupun mengambil objek. Pengaturan kekuatan dan arah "
                        + "gerakan sangat memengaruhi keberhasilan pemain. Gerakan mengambil dan berpindah "
                        + "posisi membutuhkan keseimbangan tubuh serta perpindahan berat badan yang efisien "
                        + "agar gerak tetap stabil. Posisi sendi dan koordinasi otot membantu menghasilkan "
                        + "gerakan yang cepat dan terkontrol. Aktivitas gerak cepat dan reaktif dalam Bitingan "
                        + "membantu meningkatkan agility, waktu reaksi, dan koordinasi neuromuskular pemain. "
                        + "Pola gerak multidirectional tersebut mendukung kemampuan kontrol tubuh secara "
                        + "dinamis.",
                    AsalUsul =
                        "Permainan tradisional yang dikenal dengan nama lurah-lurahan atau bitingan ini pada "
                        + "dasarnya merupakan simbol kehidupan bermasyarakat di desa. Permainan ini disimbolkan "
                        + "dengan sebuah alat, yaitu lidi (biting). Mungkin dari permainan ini yang kemudian "
                        + "menjadikan orang-orang di Jogja dan Jawa Tengah menyebut suara pada pemilihan umum "
                        + "dengan sebutan biting. Kira-kira wong kui bakal entuk biting piro? (kira-kira orang "
                        + "itu akan dapat suara berapa?)",
                    SeniBudaya =
                        "Selain belajar berhitung, ternyata ada hal lain yang bisa dipelajari anak. "
                        + "Diantaranya adalah belajar menunggu giliran, belajar sportif (dengan mengakui ada "
                        + "lidi yang goyang meski pemain lain tidak melihat), juga belajar strategi pemecahan "
                        + "masalah.",
                },
                new LessonSource
                {
                    GameKey = "ancak_ancak_alis",
                    DisplayName = "Ancak-ancak Alis",
                    VideoGroup = "Vid Ancak",
                    Sains =
                        "Interaksi antarindividu dalam membentuk pola adaptasi terhadap perubahan situasi; "
                        + "mekanisme pengambilan keputusan berdasarkan informasi lingkungan; hubungan dinamika "
                        + "gerak dan organisasi kelompok; serta pengembangan keterampilan ilmiah melalui "
                        + "observasi pola interaksi dan interpretasi strategi adaptif.",
                    SportScience =
                        "Anatomi pada Ancak-Ancak Alis = Permainan Ancak-Ancak Alis melibatkan koordinasi "
                        + "otot tangan, kaki, dan tubuh dalam aktivitas berjalan, melompat, serta bergerak "
                        + "mengikuti irama permainan. Aktivitas ini membantu meningkatkan koordinasi motorik "
                        + "dan keseimbangan tubuh pemain. Gerakan berpindah posisi dan membentuk formasi "
                        + "menggunakan kerja otot tungkai, panggul, dan otot inti untuk menjaga stabilitas "
                        + "tubuh. Selain itu, koordinasi saraf dan otot membantu pemain bergerak secara sinkron "
                        + "dengan teman bermain. Aktivitas fisik dalam Ancak-Ancak Alis mendukung perkembangan "
                        + "fleksibilitas, kelincahan, dan kontrol gerak tubuh melalui pola gerak yang dilakukan "
                        + "secara berulang. Permainan ini juga membantu meningkatkan konsentrasi dan koordinasi "
                        + "neuromuskular pemain. Biomekanika Gerak pada Ancak-Ancak Alis= Ancak-Ancak Alis "
                        + "melibatkan gerakan ritmis dan perpindahan posisi yang membutuhkan keseimbangan serta "
                        + "kontrol pusat massa tubuh. Pemain harus mampu menjaga stabilitas saat bergerak dan "
                        + "berubah arah. Gerakan berjalan, melompat, dan membentuk pola permainan menunjukkan "
                        + "prinsip biomekanika tentang koordinasi gerak, pengaturan gaya tubuh, dan efisiensi "
                        + "langkah. Ketepatan posisi tubuh sangat memengaruhi kelancaran gerakan pemain. "
                        + "Aktivitas gerak dinamis dalam Ancak-Ancak Alis membantu meningkatkan agility, "
                        + "koordinasi neuromuskular, dan kemampuan reaksi tubuh terhadap perubahan gerak. Pola "
                        + "gerak berirama tersebut juga mendukung kontrol motorik dan keseimbangan dinamis "
                        + "pemain.",
                    AsalUsul =
                        "Permainan ancak-ancak alis berasal dari bahasa Jawa yaitu dari kata ancak-ancak dan "
                        + "alis. Kata ancak berarti bujur sangkar dengan berbingkai pelepah daun pisang untuk "
                        + "tempat sesaji sedangkan kata alis dalam lagu permainan ini yang dimaksud adalah nama "
                        + "seeker kerbau. Latar belakang permainan ancak-ancak alis ini menitikberatkan kepada "
                        + "kehidupan pertanian yang sebagian besar merupakan mata pencaharian penduduk di "
                        + "Indonesia. Oleh sebab itu secara tidak langsung permainan ini mendidik anak-anak "
                        + "untuk mengetahui dunia pertanian. Misalnya, mengenalkan nama-nama tanaman, nama hama "
                        + "tanaman, cara• cara bertani dan sebagainya.",
                    SeniBudaya =
                        "Adapun iringan dalam permainan ini berupa lagu ancak-ancak alis tanpa menggunakan "
                        + "bunyi-bunyian. Syair tersebut adalah sebagai berikut:\n\n\"Ancak-ancak alis, si alis "
                        + "kebo janggitan, anak-anak kebo dhungkul, Si dhungkul bangbang teyo, tiga rendeng, "
                        + "enceng-enceng gogo beluk, unine paling cerepluk, ula sawa, ula dumung, gedhene sak "
                        + "lumbung bandhung, sawahira lagi apa ?\". Makna dari lagu ancak-ancak alis yaitu alam "
                        + "semua tumbuhan dan hewan itu bisa mendatangkan kemakmuran tetapi juga sumber "
                        + "hambatan dalam hidup baik dimusim kemarau maupun penghujan. Oleh karena itu "
                        + "interaksi saling menanyakan kabar tentang sawah ladang dan kebun serta kondisi alam "
                        + "antar warga adalah penting sehingga bisa diantisipasi bersama-sama.\n\nPermainan ini "
                        + "menunjukkan hidup tidak bisa instan, harus ada proses yang harus dilewati. Setiap "
                        + "orang harus berusaha keras untuk mencapai tujuan yang diinginkan. Melalui permainan "
                        + "ini, anak belajar untuk bekerja keras agar mendapatkan hasil yang diimpikan.\n\n",
                },
                new LessonSource
                {
                    GameKey = "blarak_sempal",
                    DisplayName = "Blarak Sempal",
                    VideoGroup = "Vid Blarak",
                    Sains =
                        "Interaksi sifat material alami dengan gaya dan gerakan; pengaruh bentuk, ukuran, "
                        + "tekstur, dan gesekan terhadap arah serta kestabilan gerak; hubungan struktur–fungsi "
                        + "pada objek alami; serta pengembangan keterampilan ilmiah melalui observasi "
                        + "karakteristik material dan interpretasi perilaku gerak.",
                    SportScience =
                        "Anatomi pada Blarak Sempal = Permainan Blarak Sempal melibatkan koordinasi otot "
                        + "tangan, kaki, dan mata dalam aktivitas berlari, melempar, serta menghindar selama "
                        + "permainan berlangsung. Aktivitas ini membantu meningkatkan kemampuan motorik kasar, "
                        + "kelincahan, dan koordinasi tubuh pemain. Gerakan melempar dan bergerak cepat "
                        + "menggunakan kerja otot bahu, lengan, tungkai, serta otot inti untuk menjaga "
                        + "keseimbangan tubuh. Sendi lutut, panggul, dan pergelangan kaki juga berperan penting "
                        + "dalam mendukung perpindahan gerak yang dinamis. Aktivitas fisik dalam Blarak Sempal "
                        + "dapat meningkatkan daya tahan tubuh, koordinasi neuromuskular, dan kontrol gerak "
                        + "karena dilakukan secara aktif dan berulang. Permainan ini juga membantu melatih "
                        + "konsentrasi serta respons gerak pemain. Biomekanika Gerak pada Blarak Sempal= Blarak "
                        + "Sempal melibatkan prinsip biomekanika berupa gaya lemparan, kecepatan gerak, dan "
                        + "keseimbangan tubuh saat bergerak maupun menghindar. Pemain harus mampu mengontrol "
                        + "pusat massa tubuh agar tetap stabil selama permainan. Gerakan melempar dan berpindah "
                        + "posisi membutuhkan koordinasi antara kekuatan otot dan pengaturan posisi sendi untuk "
                        + "menghasilkan gerakan yang efektif. Ketepatan arah dan kekuatan gerak sangat "
                        + "memengaruhi keberhasilan pemain. Aktivitas gerak cepat dan multidirectional dalam "
                        + "Blarak Sempal membantu meningkatkan agility, waktu reaksi, dan koordinasi "
                        + "neuromuskular pemain. Pola gerak tersebut mendukung efisiensi gerakan dan "
                        + "keseimbangan dinamis tubuh.",
                    AsalUsul =
                        "Kata blarak-blarak sempal berasal dari bahasa Jawa. B/arakberarti daun kelapa yang "
                        + "sudah kering dan sempal artinya patah dari batangnya maka arti keseluruhan daun "
                        + "kelapa yang patah. Latar belakang sosial budaya permainan ini merupakan alat "
                        + "pergaulan, menghilangkan, rasa malu, mendatangkan rasa senang dan lain sebagainya.",
                    SeniBudaya =
                        "Lagu blarak-blarak sempal pada lirik lagu pertama “blarak blarak sempal” ibarat "
                        + "seseorang yang seperti pohon kelapa. Tangangnya seperti daunnya yang kumleyang atau "
                        + "kumlawe. Orang tersebut akan suka menolong kepada sesame, dengan tangannya yang "
                        + "ringan itu akan suka menolong kesana kesini. Dan jika mencari ilmu akan dimudahkan "
                        + "oleh Allah SWT. Pada lirik lagu yang kedua “dincik-i mendal mendal” maksudnya yaitu "
                        + "tangan seseorang itu seperti daun kelapa (godhong krambil). Jika tangannya di "
                        + "senggol atau menatap sesuatu akan mantul-mantul. Pada lirik lagu yang ketiga dan "
                        + "keempat sama “legendre tak pancale” artinya “orang yang mencari ilmu harus ditambah "
                        + "terus ilmunya dan tidak ada habisnya”. Pada lirik lagu tersebut diulang dua kali, "
                        + "tujuannya untuk menegaskan supaya orang tidak berhenti mencari ilmu. Jika orang "
                        + "tersebut masih mampu untuk mencari ilmu kejenjang yang lebih tinggi dan menambah "
                        + "ilmunya. Pada lirik lagu yang terakhir “yen tiba tangi ya dhewe” artinya “jika jatuh "
                        + "bangun sendiri”. Orang yang ingin mencapai kesuksesan harus mampu mengalami proses "
                        + "yang rumit terlebih dahulu. kesuksesan butuh proses yang panjang, kita harus "
                        + "mengalami banyak cobaan dan ujian. Jika kita kita ingin mencapai kesuksesan itu, "
                        + "kita harus lebih banyak bersabar dan bertawakal serta tidak lupa berdo’a kepada "
                        + "Allah SWT.",
                },
            };
        }
    }
}
