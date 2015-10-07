
; Copyright (c) 2015 Microsoft Corporation
;; Rounding mode: to positive
;; Precision: double (11/53)
;; X = +1.24503981037881583660009710001759231090545654296875p-757 {+ 1103561198712972 -757 (1.64239e-228)}
;; Y = -1.3703055165615218857766421933774836361408233642578125p73 {- 1667707786399709 73 (-1.29422e+022)}
;; Z = -1.534190900553739300704592096735723316669464111328125p-732 {- 2405781940678530 -732 (-6.79083e-221)}
;; +1.24503981037881583660009710001759231090545654296875p-757 x -1.3703055165615218857766421933774836361408233642578125p73 -1.534190900553739300704592096735723316669464111328125p-732 == -1.7060849205008132845051704862271435558795928955078125p-684

;; mpf : - 3179923784859389 -684
;; mpfd: - 3179923784859389 -684 (-2.12561e-206) class: Neg. norm. non-zero
;; hwf : - 3179923784859364 -684 (-2.12561e-206) class: Neg. norm. non-zero

(set-logic QF_FP)
(set-info :status unsat)

(define-sort FPN () (_ FloatingPoint 11 53))
(declare-const x FPN)
(declare-const y FPN)
(declare-const z FPN)
(declare-const r FPN)
(declare-const q FPN)
(declare-const mpfx FPN)

(assert (= mpfx (fp.fma roundTowardPositive
		  ((_ to_fp 11 53) roundNearestTiesToEven 1.24503981037881583660009710001759231090545654296875 (- 757))
		  ((_ to_fp 11 53) roundNearestTiesToEven (- 1.3703055165615218857766421933774836361408233642578125) 73)
		  ((_ to_fp 11 53) roundNearestTiesToEven (- 1.534190900553739300704592096735723316669464111328125) (- 732)))))
;;	  ((_ to_fp 11 53) roundNearestTiesToEven (- 1.7060849205008132845051704862271435558795928955078125) (- 684))))


(assert (= x ((_ to_fp 11 53) roundNearestTiesToEven 1.24503981037881583660009710001759231090545654296875 (- 757))))
(assert (= y ((_ to_fp 11 53) roundNearestTiesToEven (- 1.3703055165615218857766421933774836361408233642578125) 73)))
(assert (= z ((_ to_fp 11 53) roundNearestTiesToEven (- 1.534190900553739300704592096735723316669464111328125) (- 732))))
(assert (= r ((_ to_fp 11 53) roundNearestTiesToEven (- 1.70608492050080773339004736044444143772125244140625) (- 684))))

(assert (= q (fp.fma roundTowardPositive x y z)))

(assert (not (= q r)))

(check-sat)
(check-sat-using smt)
