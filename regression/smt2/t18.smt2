(declare-fun p1 () Bool)
(declare-fun p2 () Bool)
(declare-fun p3 () Bool)
(declare-fun p4 () Bool)
(declare-fun a () Int)
(declare-fun b () Int)
(declare-fun c () Int)
(declare-fun d () Int)
(declare-fun e () Int)

(set-option :pp.flat-assoc false)
(echo "Testing ite...")
(simplify (ite p1 (ite p1 a b) c))
(simplify (ite p1 (ite p1 (ite p1 a b) c) d))
(simplify (ite p1 (ite p1 (ite p1 a b) c) (ite p1 d e)))
(simplify (ite p1 (ite (not p1) a b) c))
(simplify (ite p1 a (ite (not p1) b c)))
(simplify (ite true a b))
(simplify (ite false a b))
(simplify (ite p1 a a))
(simplify (ite p1 true p2))
(simplify (ite p1 false p2))
(simplify (ite p1 p2 true))
(simplify (ite p1 p2 false))
(simplify (ite p1 p1 p2))
(simplify (ite p1 p2 p1))
(simplify (ite p1 p2 (not p2)))
(simplify (ite p1 (not p2) p2))
(simplify (ite p1 (ite p2 a b) a) :ite-extra-rules true)
(simplify (ite p1 (ite p2 a b) b) :ite-extra-rules true)
(simplify (ite (not p1) a b))
(simplify (ite (not p1) (ite p1 a b) c))
(simplify (ite p1 (ite p2 a b) (ite (and p2 p3) a b)) :ite-extra-rules true :flat false)
(simplify (ite p1 (ite p2 a b) (ite (and p2 p3) a b)) :ite-extra-rules true :flat true)
(simplify (ite p1 (ite p2 a b) (ite (and p1 p3) a b)) :ite-extra-rules true)
(simplify (ite p1 (ite p2 a b) (ite (and p1 p3) a b)) :ite-extra-rules true :flat false)
(simplify (ite p1 (ite p2 a b) (ite p3 a b)) :ite-extra-rules true)
(simplify (ite p1 (ite p2 a b) (ite p3 b a)) :ite-extra-rules true)
