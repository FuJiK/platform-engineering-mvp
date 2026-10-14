(ns platform-mvp.test-runner
  (:require [clojure.test :as test]
            [platform-mvp.compiler-test]
            [platform-mvp.policy-test]
            [platform-mvp.docker-test]
            [platform-mvp.mcp-test]))

(defn -main
  [& _]
  (let [result (test/run-tests 'platform-mvp.compiler-test
                               'platform-mvp.policy-test
                               'platform-mvp.docker-test
                               'platform-mvp.mcp-test)]
    (when (pos? (+ (:fail result) (:error result)))
      (System/exit 1))))
