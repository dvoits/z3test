(set-logic AUFBV)

(set-option :auto-config true)
(set-option :smt.mbqi.max-iterations 1)

(declare-fun memset_0 ((_ BitVec 32) (_ BitVec 8) (_ BitVec 32)) (Array (_ BitVec 32) (_ BitVec 8)))
(declare-fun initialMemoryState_0xa95b430 () (Array (_ BitVec 32) (_ BitVec 8)))

(assert
(and
(forall ((?k!00 (_ BitVec 32))
         (?k!10 (_ BitVec 8))
         (?k!20 (_ BitVec 32))
         (?k!30 (_ BitVec 32)))
  (! (let ((a!1 (ite (= #x1fffffe4 ?k!30)
                     (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     (= (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30)
                        (select initialMemoryState_0xa95b430 ?k!30)))))
     (let ((a!2 (ite (= #x1fffffe5 ?k!30)
                     (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     a!1)))
     (let ((a!3 (ite (= #x1fffffe6 ?k!30)
                     (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     a!2)))
     (let ((a!4 (ite (= #x1fffffe7 ?k!30)
                     (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     a!3)))
     (let ((a!5 (ite (= #x1fffffe8 ?k!30)
                     (= #x95 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     a!4)))
     (let ((a!6 (ite (= #x1fffffe9 ?k!30)
                     (= #xdf (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     a!5)))
     (let ((a!7 (ite (= #x1fffffea ?k!30)
                     (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     a!6)))
     (let ((a!8 (ite (= #x1fffffeb ?k!30)
                     (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     a!7)))
     (let ((a!9 (ite (= #x1fffffec ?k!30)
                     (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                     a!8)))
     (let ((a!10 (ite (= #x1fffffed ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!9)))
     (let ((a!11 (ite (= #x1fffffee ?k!30)
                      (= #x50 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!10)))
     (let ((a!12 (ite (= #x1fffffef ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!11)))
     (let ((a!13 (ite (= #x1ffffff0 ?k!30)
                      (= #x04 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!12)))
     (let ((a!14 (ite (= #x1ffffff1 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!13)))
     (let ((a!15 (ite (= #x1ffffff2 ?k!30)
                      (= #x50 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!14)))
     (let ((a!16 (ite (= #x1ffffff3 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!15)))
     (let ((a!17 (ite (= #x1ffffff4 ?k!30)
                      (= #xfc (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!16)))
     (let ((a!18 (ite (= #x1ffffff5 ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!17)))
     (let ((a!19 (ite (= #x1ffffff6 ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!18)))
     (let ((a!20 (ite (= #x1ffffff7 ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!19)))
     (let ((a!21 (ite (= #x1ffffff8 ?k!30)
                      (= #x95 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!20)))
     (let ((a!22 (ite (= #x1ffffff9 ?k!30)
                      (= #xdf (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!21)))
     (let ((a!23 (ite (= #x1ffffffa ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!22)))
     (let ((a!24 (ite (= #x1ffffffb ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!23)))
     (let ((a!25 (ite (= #x1ffffffc ?k!30)
                      (= #x08 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!24)))
     (let ((a!26 (ite (= #x1ffffffd ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!25)))
     (let ((a!27 (ite (= #x1ffffffe ?k!30)
                      (= #x50 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!26)))
     (let ((a!28 (ite (= #x1fffffff ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!27)))
     (let ((a!29 (ite (= #x1ffff7d7 ?k!30)
                      (= #x37 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!28)))
     (let ((a!30 (ite (= #x1ffff7d8 ?k!30)
                      (= #x44 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!29)))
     (let ((a!31 (ite (= #x1ffff7d9 ?k!30)
                      (= #x65 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!30)))
     (let ((a!32 (ite (= #x1ffff7da ?k!30)
                      (= #x72 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!31)))
     (let ((a!33 (ite (= #x1ffff7db ?k!30)
                      (= #x69 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!32)))
     (let ((a!34 (ite (= #x1ffff7dc ?k!30)
                      (= #x76 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!33)))
     (let ((a!35 (ite (= #x1ffff7dd ?k!30)
                      (= #x65 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!34)))
     (let ((a!36 (ite (= #x1ffff7de ?k!30)
                      (= #x64 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!35)))
     (let ((a!37 (ite (= #x1ffff7df ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!36)))
     (let ((a!38 (ite (= #x1fffefcc ?k!30)
                      (= #x35 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!37)))
     (let ((a!39 (ite (= #x1fffefcd ?k!30)
                      (= #x42 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!38)))
     (let ((a!40 (ite (= #x1fffefce ?k!30)
                      (= #x61 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!39)))
     (let ((a!41 (ite (= #x1fffefcf ?k!30)
                      (= #x73 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!40)))
     (let ((a!42 (ite (= #x1fffefd0 ?k!30)
                      (= #x65 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!41)))
     (let ((a!43 (ite (= #x1fffefd1 ?k!30)
                      (= #x31 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!42)))
     (let ((a!44 (ite (= #x1fffefd2 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!43)))
     (let ((a!45 (ite (= #x1fffebc4 ?k!30)
                      (= #xdb (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!44)))
     (let ((a!46 (ite (= #x1fffebc5 ?k!30)
                      (= #xf3 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!45)))
     (let ((a!47 (ite (= #x1fffebc6 ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!46)))
     (let ((a!48 (ite (= #x1fffebc7 ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!47)))
     (let ((a!49 (ite (= #x1fffebc8 ?k!30)
                      (= #xcc (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!48)))
     (let ((a!50 (ite (= #x1fffebc9 ?k!30)
                      (= #xef (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!49)))
     (let ((a!51 (ite (= #x1fffebca ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!50)))
     (let ((a!52 (ite (= #x1fffebcb ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!51)))
     (let ((a!53 (ite (= #x1fffe7bd ?k!30)
                      (= #x35 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!52)))
     (let ((a!54 (ite (= #x1fffe7be ?k!30)
                      (= #x42 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!53)))
     (let ((a!55 (ite (= #x1fffe7bf ?k!30)
                      (= #x61 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!54)))
     (let ((a!56 (ite (= #x1fffe7c0 ?k!30)
                      (= #x73 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!55)))
     (let ((a!57 (ite (= #x1fffe7c1 ?k!30)
                      (= #x65 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!56)))
     (let ((a!58 (ite (= #x1fffe7c2 ?k!30)
                      (= #x32 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!57)))
     (let ((a!59 (ite (= #x1fffe7c3 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!58)))
     (let ((a!60 (ite (= #x1fffe3b5 ?k!30)
                      (= #xdb (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!59)))
     (let ((a!61 (ite (= #x1fffe3b6 ?k!30)
                      (= #xf3 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!60)))
     (let ((a!62 (ite (= #x1fffe3b7 ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!61)))
     (let ((a!63 (ite (= #x1fffe3b8 ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!62)))
     (let ((a!64 (ite (= #x1fffe3b9 ?k!30)
                      (= #xbd (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!63)))
     (let ((a!65 (ite (= #x1fffe3ba ?k!30)
                      (= #xe7 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!64)))
     (let ((a!66 (ite (= #x1fffe3bb ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!65)))
     (let ((a!67 (ite (= #x1fffe3bc ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!66)))
     (let ((a!68 (ite (= #x1fffdf95 ?k!30)
                      (= #xe8 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!67)))
     (let ((a!69 (ite (= #x1fffdf96 ?k!30)
                      (= #xfb (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!68)))
     (let ((a!70 (ite (= #x1fffdf97 ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!69)))
     (let ((a!71 (ite (= #x1fffdf98 ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!70)))
     (let ((a!72 (ite (= #x1fffdf99 ?k!30)
                      (= #xd7 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!71)))
     (let ((a!73 (ite (= #x1fffdf9a ?k!30)
                      (= #xf7 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!72)))
     (let ((a!74 (ite (= #x1fffdf9b ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!73)))
     (let ((a!75 (ite (= #x1fffdf9c ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!74)))
     (let ((a!76 (ite (= #x1fffdf9d ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!75)))
     (let ((a!77 (ite (= #x1fffdf9e ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!76)))
     (let ((a!78 (ite (= #x1fffdf9f ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!77)))
     (let ((a!79 (ite (= #x1fffdfa0 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!78)))
     (let ((a!80 (ite (= #x1fffdfa1 ?k!30)
                      (= #x02 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!79)))
     (let ((a!81 (ite (= #x1fffdfa2 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!80)))
     (let ((a!82 (ite (= #x1fffdfa3 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!81)))
     (let ((a!83 (ite (= #x1fffdfa4 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!82)))
     (let ((a!84 (ite (= #x1fffdfa5 ?k!30)
                      (= #xc4 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!83)))
     (let ((a!85 (ite (= #x1fffdfa6 ?k!30)
                      (= #xeb (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!84)))
     (let ((a!86 (ite (= #x1fffdfa7 ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!85)))
     (let ((a!87 (ite (= #x1fffdfa8 ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!86)))
     (let ((a!88 (ite (= #x1fffdfa9 ?k!30)
                      (= #x02 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!87)))
     (let ((a!89 (ite (= #x1fffdfaa ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!88)))
     (let ((a!90 (ite (= #x1fffdfab ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!89)))
     (let ((a!91 (ite (= #x1fffdfac ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!90)))
     (let ((a!92 (ite (= #x1fffdfad ?k!30)
                      (= #xb5 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!91)))
     (let ((a!93 (ite (= #x1fffdfae ?k!30)
                      (= #xe3 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!92)))
     (let ((a!94 (ite (= #x1fffdfaf ?k!30)
                      (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!93)))
     (let ((a!95 (ite (= #x1fffdfb0 ?k!30)
                      (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!94)))
     (let ((a!96 (ite (= #x1fffdfb1 ?k!30)
                      (= #x02 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!95)))
     (let ((a!97 (ite (= #x1fffdfb2 ?k!30)
                      (= #x04 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!96)))
     (let ((a!98 (ite (= #x1fffdfb3 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!97)))
     (let ((a!99 (ite (= #x1fffdfb4 ?k!30)
                      (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                      a!98)))
     (let ((a!100 (ite (= #x1fffdb89 ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!99)))
     (let ((a!101 (ite (= #x1fffdb8a ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!100)))
     (let ((a!102 (ite (= #x1fffdb8b ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!101)))
     (let ((a!103 (ite (= #x1fffdb8c ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!102)))
     (let ((a!104 (ite (= #x1fffdb8d ?k!30)
                       (= #xb5 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!103)))
     (let ((a!105 (ite (= #x1fffdb8e ?k!30)
                       (= #xe3 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!104)))
     (let ((a!106 (ite (= #x1fffdb8f ?k!30)
                       (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!105)))
     (let ((a!107 (ite (= #x1fffdb90 ?k!30)
                       (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!106)))
     (let ((a!108 (ite (= #x1fffdb91 ?k!30)
                       (= #x0c (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!107)))
     (let ((a!109 (ite (= #x1fffdb92 ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!108)))
     (let ((a!110 (ite (= #x1fffdb93 ?k!30)
                       (= #x50 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!109)))
     (let ((a!111 (ite (= #x1fffdb94 ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!110)))
     (let ((a!112 (ite (= #x1fffd77d ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!111)))
     (let ((a!113 (ite (= #x1fffd77e ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!112)))
     (let ((a!114 (ite (= #x1fffd77f ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!113)))
     (let ((a!115 (ite (= #x1fffd780 ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!114)))
     (let ((a!116 (ite (= #x1fffd781 ?k!30)
                       (= #xc4 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!115)))
     (let ((a!117 (ite (= #x1fffd782 ?k!30)
                       (= #xeb (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!116)))
     (let ((a!118 (ite (= #x1fffd783 ?k!30)
                       (= #xff (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!117)))
     (let ((a!119 (ite (= #x1fffd784 ?k!30)
                       (= #x1f (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!118)))
     (let ((a!120 (ite (= #x1fffd785 ?k!30)
                       (= #x10 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!119)))
     (let ((a!121 (ite (= #x1fffd786 ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!120)))
     (let ((a!122 (ite (= #x1fffd787 ?k!30)
                       (= #x50 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!121)))
     (let ((a!123 (ite (= #x1fffd788 ?k!30)
                       (= #x00 (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))
                       a!122)))
       (ite (or (not (bvule ?k!00 ?k!30)) (bvule (bvadd ?k!00 ?k!20) ?k!30))
            a!123
            (= (select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30) ?k!10)))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))
     :pattern ((select (memset_0 ?k!00 ?k!10 ?k!20) ?k!30))))
(let ((a!1 (not (= #x00
                   (select (memset_0 #x7ffffff8 #x00 #x00000008) #x1ffffffc))))
      (a!2 (not (= #x00
                   (select (memset_0 #x7ffffff8 #x00 #x00000008) #x1ffffffd))))
      (a!3 (not (= #x50
                   (select (memset_0 #x7ffffff8 #x00 #x00000008) #x1ffffffe))))
      (a!4 (not (= #x00
                   (select (memset_0 #x7ffffff8 #x00 #x00000008) #x1fffffff))))
      (a!5 (not (= #x04
                   (select (memset_0 #x7ffffff8 #x00 #x00000008) #x1ffffffc))))
      (a!6 (not (= #x08
                   (select (memset_0 #x7ffffff8 #x00 #x00000008) #x1ffffffc))))
      (a!7 (not (= #x0c
                   (select (memset_0 #x7ffffff8 #x00 #x00000008) #x1ffffffc))))
      (a!8 (not (= #x10
                   (select (memset_0 #x7ffffff8 #x00 #x00000008) #x1ffffffc)))))
(let ((a!9 (not (or (not (or a!1 a!2 a!3 a!4))
                    (not (or a!5 a!2 a!3 a!4))
                    (not (or a!6 a!2 a!3 a!4))
                    (not (or a!7 a!2 a!3 a!4))
                    (not (or a!8 a!2 a!3 a!4)))))
      (a!10 (not (or (not (or a!1 a!2 a!3 a!4))
                     (not (or a!5 a!2 a!3 a!4))
                     (not (or a!6 a!2 a!3 a!4))
                     (not (or a!7 a!2 a!3 a!4))
                     a!8
                     a!2
                     a!3
                     a!4)))
      (a!11 (not (or (not (or a!1 a!2 a!3 a!4))
                     (not (or a!5 a!2 a!3 a!4))
                     (not (or a!6 a!2 a!3 a!4))
                     a!7
                     a!2
                     a!3
                     a!4)))
      (a!12 (not (or (not (or a!1 a!2 a!3 a!4))
                     (not (or a!5 a!2 a!3 a!4))
                     a!6
                     a!2
                     a!3
                     a!4)))
      (a!13 (not (or (not (or a!1 a!2 a!3 a!4)) a!5 a!2 a!3 a!4))))
  (or a!9 a!10 a!11 a!12 a!13 (not (or a!1 a!2 a!3 a!4)))))
))

(check-sat)
