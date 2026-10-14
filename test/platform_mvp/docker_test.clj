(ns platform-mvp.docker-test
  (:require [clojure.test :refer [deftest is testing]]
            [platform-mvp.docker :as docker]))

(deftest run-process-captures-stdout
  (let [{:keys [exit out err]} (docker/run-process ["echo" "hello-mvp"])]
    (is (zero? exit))
    (is (= "hello-mvp" (clojure.string/trim out)))
    (is (clojure.string/blank? err))))

(deftest run-process-surfaces-nonzero-exit
  (let [{:keys [exit out err]} (docker/run-process ["sh" "-c" "echo oops 1>&2; exit 42"])]
    (is (= 42 exit))
    (is (= "oops" (clojure.string/trim err)))))

(deftest require-success-throws-with-process-details
  (is (thrown-with-msg? clojure.lang.ExceptionInfo
                        #"inspect failed"
                        (docker/require-success!
                         {:exit 1 :out "" :err "container not found"}
                         "docker inspect"))))

(deftest get-status-uses-docker-inspect
  (with-redefs [docker/run-process
                (fn [args]
                  (is (= ["docker" "inspect" "--format" "{{.State.Status}}" "demo-web"]
                         args))
                  {:exit 0 :out "running\n" :err ""})]
    (is (= "running" (docker/get-status "demo-web")))))
