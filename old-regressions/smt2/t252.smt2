


(declare-const x0 Real)
(declare-const x1 Real)


;; - x0^9 x1^2 - 9 x0^7 x1^2 - 27 x0^5 x1^2 - 27 x0^3 x1^2 + 10 x0^8 x1 + 72 x0^6 x1 + 162 x0^4 x1 + 108 x0^2 x1 + 4 x0^9 + 15 x0^7 + 9 x0^5
(poly/factor (+ (* (- 1.) (^ x0 9.) (^ x1 2.))
                (* (- 9.) (^ x0 7.) (^ x1 2.))
                (* (- 27.) (^ x0 5.) (^ x1 2.))
                (* (- 27.) (^ x0 3.) (^ x1 2.))
                (* 10. (^ x0 8.) x1)
                (* 72. (^ x0 6.) x1)
                (* 162. (^ x0 4.) x1)
                (* 108. (^ x0 2.) x1)
                (* 4. (^ x0 9.))
                (* 15. (^ x0 7.))
                (* 9. (^ x0 5.))))

(simplify (=
           (*
            (+ (^ x0 2.0) 3.0)
            (^ x0 2.0)
            (+ (* (- 1.0) (^ x0 5.0) (^ x1 2.0))
               (* (- 6.0) (^ x0 3.0) (^ x1 2.0))
               (* 10.0 (^ x0 4.0) x1)
               (* 4.0 (^ x0 5.0))
               (* (- 9.0) x0 (^ x1 2.0))
               (* 42.0 (^ x0 2.0) x1)
               (* 3.0 (^ x0 3.0))
               (* 36.0 x1)))
           (+ (* (- 1.) (^ x0 9.) (^ x1 2.))
              (* (- 9.) (^ x0 7.) (^ x1 2.))
              (* (- 27.) (^ x0 5.) (^ x1 2.))
              (* (- 27.) (^ x0 3.) (^ x1 2.))
              (* 10. (^ x0 8.) x1)
              (* 72. (^ x0 6.) x1)
              (* 162. (^ x0 4.) x1)
              (* 108. (^ x0 2.) x1)
              (* 4. (^ x0 9.))
              (* 15. (^ x0 7.))
              (* 9. (^ x0 5.))))
          :som true
          :arith-lhs true
          :expand-power true)
          
