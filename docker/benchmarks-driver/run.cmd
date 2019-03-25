@echo off

: Traces collected by container will be written to the current directory
REM docker run -it --rm -v %cd%:/traces benchmarks-driver %*
docker run -it --rm benchmarks-driver %*
