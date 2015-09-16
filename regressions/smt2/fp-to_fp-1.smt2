(set-info :status unsat)
(set-info :source "Handcrafted by C.M. Wintersteiger")

(declare-fun X () (_ FloatingPoint 5 11))

(assert (= X ((_ to_fp 5 11) RTZ #x00))) ; +0.0
(assert (not (= X (_ +zero 5 11))))

(check-sat)
(exit)