(ns platform-mvp.policy-test
  (:require [clojure.test :refer [deftest is testing]]
            [platform-mvp.policy :as policy]))

(def valid-domain
  {:service :demo-web
   :environment :local
   :container {:image "nginx:alpine"
               :host-port 8080
               :container-port 80}
   :operations
   {:get-status {:roles #{:operator :sre}}
    :get-logs {:roles #{:operator :sre}
               :max-lines 200}
    :restart-service {:roles #{:sre}
                      :approval :required}}})

(deftest validate-domain-accepts-valid-input
  (is (= valid-domain (policy/validate-domain! valid-domain))))

(deftest validate-domain-rejects-non-map
  (is (thrown-with-msg? clojure.lang.ExceptionInfo
                        #"Domain definition must be a map"
                        (policy/validate-domain! "not-a-map"))))

(deftest validate-domain-rejects-invalid-container-port
  (is (thrown-with-msg? clojure.lang.ExceptionInfo
                        #"host-port"
                        (policy/validate-domain!
                         (assoc-in valid-domain [:container :host-port] 70000)))))

(deftest validate-domain-rejects-restart-without-approval
  (is (thrown-with-msg? clojure.lang.ExceptionInfo
                        #"restart-service must require approval"
                        (policy/validate-domain!
                         (assoc-in valid-domain [:operations :restart-service :approval] :optional)))))

(deftest validate-domain-rejects-invalid-get-logs-max-lines
  (is (thrown-with-msg? clojure.lang.ExceptionInfo
                        #"get-logs :max-lines"
                        (policy/validate-domain!
                         (assoc-in valid-domain [:operations :get-logs :max-lines] 0)))))

(deftest role-allowed-accepts-string-and-keyword-roles
  (let [ops-policy {:operations
                    {:get-status {:roles ["operator" "sre"]}
                     :restart-service {:roles ["sre"]}}}]
    (is (policy/role-allowed? ops-policy "operator" :get-status))
    (is (policy/role-allowed? ops-policy :sre :restart-service))
    (is (not (policy/role-allowed? ops-policy :operator :restart-service)))))

(deftest role-allowed-denies-unknown-operation
  (is (not (policy/role-allowed? {:operations {}} :operator :get-status))))
