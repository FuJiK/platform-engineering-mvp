(ns platform-mvp.compiler-test
  (:require [clojure.test :refer [deftest is testing]]
            [platform-mvp.compiler :as compiler]
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

(deftest validates-domain
  (is (= valid-domain (policy/validate-domain! valid-domain))))

(deftest rejects-dangerous-unknown-operation
  (is (thrown-with-msg?
       clojure.lang.ExceptionInfo
       #"Unsupported operation"
       (policy/validate-domain!
        (assoc-in valid-domain [:operations :delete-service]
                  {:roles #{:sre}})))))

(deftest generates-terraform-json-shape
  (let [tf (compiler/build-terraform-json valid-domain)]
    (is (= "nginx:alpine"
           (get-in tf [:resource :docker_image :web :name])))
    (is (= "demo-web"
           (get-in tf [:resource :docker_container :web :name])))
    (is (= 8080
           (get-in tf [:resource :docker_container :web :ports 0 :external])))))

(deftest generates-ops-policy
  (let [ops (compiler/build-ops-policy valid-domain)]
    (is (= "demo-web" (:service ops)))
    (is (= ["sre"]
           (get-in ops [:operations "restart-service" :roles])))
    (is (= "required"
           (get-in ops [:operations "restart-service" :approval])))))

(deftest role-allowed-matches-json-string-roles
  (let [ops-policy {:operations
                    {:get-status {:roles ["operator" "sre"]}
                     :get-logs {:roles ["operator" "sre"]}
                     :restart-service {:roles ["sre"]}}}]
    (is (policy/role-allowed? ops-policy :operator :get-status))
    (is (policy/role-allowed? ops-policy :sre :restart-service))
    (is (not (policy/role-allowed? ops-policy :operator :restart-service)))))
