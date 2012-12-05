from z3 import *
set_option(auto_config=True)

x = Real('x')
y = Real('y')
solve(x**2 + y**2 > 3, x**3 + y < 5)
