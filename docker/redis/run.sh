#!/usr/bin/env bash

#echo on
set -x

docker run \
    -d \
    --log-opt max-size=10m \
    --log-opt max-file=3 \
    --name benchmarks-redis \
    --network host \
    --restart always \
    benchmarks-redis
