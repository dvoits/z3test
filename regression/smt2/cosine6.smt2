
(set-option :numeral-as-real true)
(declare-const x Real)
(declare-const y Real)
(declare-const a Int)

(simplify (cos (+ x (* 2 pi) y (* 2 pi (to_real a)))))
(simplify (cos (+ x (* 2 pi) y (* 4 pi (to_real a)))))
(simplify (cos (+ x (* 2 pi) y (* (- 4) pi (to_real a)))))
(simplify (cos (+ x (* 2 pi) y (* (- 2) pi (to_real a)))))
(simplify (cos (+ x (* 2 pi) y (* pi (to_real a)))))

(simplify (cos (+ x (* 2 pi) y (* 2 (to_real a) pi))))
(simplify (cos (+ x (* 2 pi) y (* 4 (to_real a) pi))))

