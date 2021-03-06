
; Copyright (c) 2015 Microsoft Corporation
(set-info :status unsat)
(set-info :source "Handcrafted by C.M. Wintersteiger")

(define-fun X () (_ FloatingPoint  8 24) ((_ to_fp 8 24) RNE (/ 11459215 16777216)))
(declare-fun Y () (_ FloatingPoint  8 24))
(declare-fun R () (_ BitVec 8))

(assert (and
			(fp.eq Y X)
			(= R ((_ fp.to_sbv 8) RNE Y))			
			(not (= R #x01))
		))

(check-sat)
(check-sat-using smt)
(exit)
