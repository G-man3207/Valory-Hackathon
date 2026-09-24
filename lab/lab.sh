#!/usr/bin/env bash
set -euo pipefail
trap 'echo "Lab command failed at line $LINENO; inspect lab/lab.sh status and evidence." >&2' ERR
cd "$(dirname "$0")"
readonly kubeconfig=/home/dev/.config/incident-commander/lab.kubeconfig
readonly context=kind-incident-lab
readonly namespace=incident-lab
readonly base_url=http://127.0.0.1:19080
readonly faults=(http://missing-service/inventory http://inventory:81/inventory http://inventory/missing)
kube=(kubectl --kubeconfig "$kubeconfig" --context "$context" --namespace "$namespace" --request-timeout=10s)

rollout() {
  "${kube[@]}" rollout status deployment/checkout --timeout=90s
}

set_dependency() {
  "${kube[@]}" set env deployment/checkout --containers=app "INVENTORY_URL=$1"
  rollout
}

assert_status() {
  local expected=$1 actual
  for ((attempt=0; attempt<15; attempt++)); do
    if ! actual=$(curl --silent --show-error --max-time 4 \
      --output /dev/null --write-out '%{http_code}' "$base_url/checkout"); then
      actual=000
    fi
    if [[ "$actual" == "$expected" ]]; then
      echo "Checkout returned expected HTTP $expected."
      return
    fi
    sleep 1
  done
  echo "Expected checkout HTTP $expected, got $actual." >&2
  return 1
}

case "${1:-status}" in
  seed)
    "${kube[@]}" get nodes >/dev/null
    timeout 180s docker build --tag incident-lab:local .
    timeout 90s kind load docker-image --name incident-lab incident-lab:local
    "${kube[@]}" apply -f workloads.yaml
    "${kube[@]}" rollout restart deployment/inventory deployment/checkout
    "${kube[@]}" rollout status deployment/inventory --timeout=90s
    rollout
    assert_status 200
    ;;
  break)
    # Random choice is bounded to known recoverable dependency configurations.
    set_dependency "${faults[RANDOM % ${#faults[@]}]}"
    assert_status 503
    ;;
  recover)
    set_dependency http://inventory/inventory
    assert_status 200
    ;;
  status)
    "${kube[@]}" get deployments,pods,services
    curl --silent --show-error --max-time 4 --write-out '\nHTTP %{http_code}\n' "$base_url/checkout"
    ;;
  evidence)
    "${kube[@]}" get deployment checkout -o json
    "${kube[@]}" get services,endpointslices
    "${kube[@]}" logs deployment/checkout --tail=50 --timestamps=true
    "${kube[@]}" get events --sort-by=.metadata.creationTimestamp
    ;;
  check)
    set_dependency http://inventory/inventory
    assert_status 200
    trap 'set_dependency http://inventory/inventory' EXIT
    for fault in "${faults[@]}"; do
      set_dependency "$fault"
      assert_status 503
      set_dependency http://inventory/inventory
      assert_status 200
    done
    trap - EXIT
    echo 'All three real dependency failures and recoveries passed.'
    ;;
  *)
    echo "Usage: $0 {seed|break|recover|status|evidence|check}" >&2
    exit 2
    ;;
esac
